using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ProgressItem = System.Collections.Generic.KeyValuePair<long, float>;

namespace BetterHI3Launcher
{
    public class BpUtility
    {
        public static void StartProcess(string proccess, string arguments, string workingDir, bool useShellExec)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(proccess, arguments);
            startInfo.WorkingDirectory = workingDir;
            startInfo.UseShellExecute = useShellExec;
            Process.Start(startInfo);
        }

        // https://stackoverflow.com/a/10520086/7570821
        public static string CalculateMD5(string filename)
        {
            using(var md5 = MD5.Create())
            {
                using(var stream = File.OpenRead(filename))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", String.Empty);
                }
            }
        }

        // https://stackoverflow.com/a/49535675/7570821
        public static string ToBytesCount(long bytes)
        {
            int unit = 1024;
            string unitStr = MainWindow.textStrings["binary_prefix_byte"];
            if(bytes < unit) return string.Format("{0} {1}", bytes, unitStr);
            else unitStr = unitStr.ToUpper();
            int exp = (int)(Math.Log(bytes) / Math.Log(unit));
            return string.Format("{0:##.##} {1}{2}", bytes / Math.Pow(unit, exp), MainWindow.textStrings["binary_prefixes"][exp - 1], unitStr);
        }

        public static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using(FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch(IOException)
            {
                return true;
            }
            return false;
        }
    }
    

    // https://stackoverflow.com/a/42725580/7570821
    public class DownloadProgressTracker
    {
        private long _totalFileSize;
        private readonly int _sampleSize;
        private readonly TimeSpan _valueDelay;

        private DateTime _lastUpdateCalculated;
        private long _previousProgress;

        private double _cachedSpeed;

        private Queue<Tuple<DateTime, long>> _changes = new Queue<Tuple<DateTime, long>>();

        public DownloadProgressTracker(int sampleSize, TimeSpan valueDelay)
        {
            _lastUpdateCalculated = DateTime.Now;
            _sampleSize = sampleSize;
            _valueDelay = valueDelay;
        }

        public void NewFile()
        {
            _previousProgress = 0;
        }

        public void SetProgress(long bytesReceived, long totalBytesToReceive)
        {
            _totalFileSize = totalBytesToReceive;

            long diff = bytesReceived - _previousProgress;
            if(diff <= 0)
                return;

            _previousProgress = bytesReceived;

            _changes.Enqueue(new Tuple<DateTime, long>(DateTime.Now, diff));
            while(_changes.Count > _sampleSize)
                _changes.Dequeue();
        }

        public double GetProgress()
        {
            return _previousProgress / (double)_totalFileSize;
        }

        public string GetProgressString()
        {
            return string.Format("{0:P0}", GetProgress());
        }

        public string GetBytesPerSecondString()
        {
            double speed = GetBytesPerSecond();
            string[] prefix;
            switch(MainWindow.OSLanguage)
            {
                case "ru-RU":
                case "uk-UA":
                case "be-BY":
                    prefix = new[]{"", "К", "М", "Г"};
                    break;
                default:
                    prefix = new[]{"", "K", "M", "G"};
                    break;
            }

            int index = 0;
            while(speed > 1024 && index < prefix.Length - 1)
            {
                speed /= 1024;
                index++;
            }

            int intLen = ((int)speed).ToString().Length;
            int decimals = 3 - intLen;
            if(decimals < 0)
                decimals = 0;

            string format = string.Format("{{0:F{0}}}", decimals) + " {1}" + MainWindow.textStrings["bytes_per_second"];

            return string.Format(format, speed, prefix[index]);
        }

        public double GetBytesPerSecond()
        {
            if(DateTime.Now >= _lastUpdateCalculated + _valueDelay)
            {
                _lastUpdateCalculated = DateTime.Now;
                _cachedSpeed = GetRateInternal();
            }

            return _cachedSpeed;
        }

        private double GetRateInternal()
        {
            if(_changes.Count == 0)
                return 0;

            TimeSpan timespan = _changes.Last().Item1 - _changes.First().Item1;
            long bytes = _changes.Sum(t => t.Item2);

            double rate = bytes / timespan.TotalSeconds;

            if(double.IsInfinity(rate) || double.IsNaN(rate))
                return 0;

            return rate;
        }
    }

    // https://gist.github.com/yasirkula/d0ec0c07b138748e5feaecbd93b6223c
    public class GoogleDriveFileDownloader : IDisposable
    {
        private const string GOOGLE_DRIVE_DOMAIN = "drive.google.com";
        private const string GOOGLE_DRIVE_DOMAIN2 = "https://drive.google.com";

        // In the worst case, it is necessary to send 3 download requests to the Drive address
        //   1. an NID cookie is returned instead of a download_warning cookie
        //   2. download_warning cookie returned
        //   3. the actual file is downloaded
        private const int GOOGLE_DRIVE_MAX_DOWNLOAD_ATTEMPT = 3;

        public delegate void DownloadProgressChangedEventHandler(object sender, DownloadProgress progress);

        // Custom download progress reporting (needed for Google Drive)
        public class DownloadProgress
        {
            public long BytesReceived, TotalBytesToReceive;
            public object UserState;

            public int ProgressPercentage
            {
                get
                {
                    if(TotalBytesToReceive > 0L)
                        return (int)(((double)BytesReceived / TotalBytesToReceive) * 100);

                    return 0;
                }
            }
        }

        // Web client that preserves cookies (needed for Google Drive)
        private class CookieAwareWebClient : WebClient
        {
            private class CookieContainer
            {
                private readonly Dictionary<string, string> cookies = new Dictionary<string, string>();

                public string this[Uri address]
                {
                    get
                    {
                        string cookie;
                        if(cookies.TryGetValue(address.Host, out cookie))
                            return cookie;

                        return null;
                    }
                    set
                    {
                        cookies[address.Host] = value;
                    }
                }
            }

            private readonly CookieContainer cookies = new CookieContainer();
            public DownloadProgress ContentRangeTarget;

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if(request is HttpWebRequest)
                {
                    string cookie = cookies[address];
                    if(cookie != null)
                        ((HttpWebRequest)request).Headers.Set("cookie", cookie);

                    if(ContentRangeTarget != null)
                        ((HttpWebRequest)request).AddRange(0);
                }

                return request;
            }

            protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
            {
                return ProcessResponse(base.GetWebResponse(request, result));
            }

            protected override WebResponse GetWebResponse(WebRequest request)
            {
                return ProcessResponse(base.GetWebResponse(request));
            }

            private WebResponse ProcessResponse(WebResponse response)
            {
                string[] cookies = response.Headers.GetValues("Set-Cookie");
                if(cookies != null && cookies.Length > 0)
                {
                    int length = 0;
                    for(int i = 0; i < cookies.Length; i++)
                        length += cookies[i].Length;

                    StringBuilder cookie = new StringBuilder(length);
                    for(int i = 0; i < cookies.Length; i++)
                        cookie.Append(cookies[i]);

                    this.cookies[response.ResponseUri] = cookie.ToString();
                }

                if(ContentRangeTarget != null)
                {
                    string[] rangeLengthHeader = response.Headers.GetValues("Content-Range");
                    if(rangeLengthHeader != null && rangeLengthHeader.Length > 0)
                    {
                        int splitIndex = rangeLengthHeader[0].LastIndexOf('/');
                        if(splitIndex >= 0 && splitIndex < rangeLengthHeader[0].Length - 1)
                        {
                            long length;
                            if(long.TryParse(rangeLengthHeader[0].Substring(splitIndex + 1), out length))
                                ContentRangeTarget.TotalBytesToReceive = length;
                        }
                    }
                }

                return response;
            }
        }

        private readonly CookieAwareWebClient webClient;
        private readonly DownloadProgress downloadProgress;

        private Uri downloadAddress;
        private string downloadPath;

        private bool asyncDownload;
        private object userToken;

        private bool downloadingDriveFile;
        private int driveDownloadAttempt;

        public event DownloadProgressChangedEventHandler DownloadProgressChanged;
        public event AsyncCompletedEventHandler DownloadFileCompleted;

        public GoogleDriveFileDownloader()
        {
            webClient = new CookieAwareWebClient();
            webClient.Headers.Add(HttpRequestHeader.UserAgent, MainWindow.userAgent);
            webClient.DownloadProgressChanged += DownloadProgressChangedCallback;
            webClient.DownloadFileCompleted += DownloadFileCompletedCallback;

            downloadProgress = new DownloadProgress();
        }

        public void DownloadFile(string address, string fileName)
        {
            DownloadFile(address, fileName, false, null);
        }

        public void DownloadFileAsync(string address, string fileName, object userToken = null)
        {
            DownloadFile(address, fileName, true, userToken);
        }

        private void DownloadFile(string address, string fileName, bool asyncDownload, object userToken)
        {
            downloadingDriveFile = address.StartsWith(GOOGLE_DRIVE_DOMAIN) || address.StartsWith(GOOGLE_DRIVE_DOMAIN2);
            if(downloadingDriveFile)
            {
                address = GetGoogleDriveDownloadAddress(address);
                driveDownloadAttempt = 1;

                webClient.ContentRangeTarget = downloadProgress;
            }
            else
                webClient.ContentRangeTarget = null;

            downloadAddress = new Uri(address);
            downloadPath = fileName;

            downloadProgress.TotalBytesToReceive = -1L;
            downloadProgress.UserState = userToken;

            this.asyncDownload = asyncDownload;
            this.userToken = userToken;

            DownloadFileInternal();
        }

        private void DownloadFileInternal()
        {
            if(!asyncDownload)
            {
                webClient.DownloadFile(downloadAddress, downloadPath);

                // This callback isn't triggered for synchronous downloads, manually trigger it
                DownloadFileCompletedCallback(webClient, new AsyncCompletedEventArgs(null, false, null));
            }
            else if(userToken == null)
                webClient.DownloadFileAsync(downloadAddress, downloadPath);
            else
                webClient.DownloadFileAsync(downloadAddress, downloadPath, userToken);
        }

        private void DownloadProgressChangedCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            if(DownloadProgressChanged != null)
            {
                downloadProgress.BytesReceived = e.BytesReceived;
                if(e.TotalBytesToReceive > 0L)
                    downloadProgress.TotalBytesToReceive = e.TotalBytesToReceive;

                DownloadProgressChanged(this, downloadProgress);
            }
        }

        private void DownloadFileCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            if(!downloadingDriveFile)
            {
                if(DownloadFileCompleted != null)
                    DownloadFileCompleted(this, e);
            }
            else
            {
                if(driveDownloadAttempt < GOOGLE_DRIVE_MAX_DOWNLOAD_ATTEMPT && !ProcessDriveDownload())
                {
                    // Try downloading the Drive file again
                    driveDownloadAttempt++;
                    DownloadFileInternal();
                }
                else if(DownloadFileCompleted != null)
                    DownloadFileCompleted(this, e);
            }
        }

        // Downloading large files from Google Drive prompts a warning screen and requires manual confirmation
        // Consider that case and try to confirm the download automatically if warning prompt occurs
        // Returns true, if no more download requests are necessary
        private bool ProcessDriveDownload()
        {
            FileInfo downloadedFile = new FileInfo(downloadPath);
            if(downloadedFile == null)
                return true;

            // Confirmation page is around 50KB, shouldn't be larger than 60KB
            if(downloadedFile.Length > 60000L)
                return true;

            // Downloaded file might be the confirmation page, check it
            string content;
            using(var reader = downloadedFile.OpenText())
            {
                // Confirmation page starts with <!DOCTYPE html>, which can be preceeded by a newline
                char[] header = new char[20];
                int readCount = reader.ReadBlock(header, 0, 20);
                if(readCount < 20 || !(new string(header).Contains("<!DOCTYPE html>")))
                    return true;

                content = reader.ReadToEnd();
            }

            int linkIndex = content.LastIndexOf("href=\"/uc?");
            if(linkIndex < 0)
                return true;

            linkIndex += 6;
            int linkEnd = content.IndexOf('"', linkIndex);
            if(linkEnd < 0)
                return true;

            downloadAddress = new Uri("https://drive.google.com" + content.Substring(linkIndex, linkEnd - linkIndex).Replace("&amp;", "&"));
            return false;
        }

        // Handles the following formats (links can be preceeded by https://):
        // - drive.google.com/open?id=FILEID
        // - drive.google.com/file/d/FILEID/view?usp=sharing
        // - drive.google.com/uc?id=FILEID&export=download
        private string GetGoogleDriveDownloadAddress(string address)
        {
            int index = address.IndexOf("id=");
            int closingIndex;
            if(index > 0)
            {
                index += 3;
                closingIndex = address.IndexOf('&', index);
                if(closingIndex < 0)
                    closingIndex = address.Length;
            }
            else
            {
                index = address.IndexOf("file/d/");
                if(index < 0) // address is not in any of the supported forms
                    return string.Empty;

                index += 7;

                closingIndex = address.IndexOf('/', index);
                if(closingIndex < 0)
                {
                    closingIndex = address.IndexOf('?', index);
                    if(closingIndex < 0)
                        closingIndex = address.Length;
                }
            }

            return string.Concat("https://drive.google.com/uc?id=", address.Substring(index, closingIndex - index), "&export=download");
        }

        public void Dispose()
        {
            webClient.Dispose();
        }
    }

    // https://stackoverflow.com/a/62039306/7570821
    public class DownloadPauseable
    {
        private volatile bool _allowedToRun;
        private readonly string _sourceUrl;
        private readonly string _destination;
        private readonly int _chunkSize;
        private readonly IProgress<double> _progress;
        private readonly Lazy<long> _contentLength;

        public long BytesWritten { get; private set; }
        public long ContentLength => _contentLength.Value;

        public bool Done => ContentLength == BytesWritten;

        public DownloadPauseable(string source, string destination, int chunkSizeInBytes = 8192, IProgress<double> progress = null)
        {
            if(string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
                throw new ArgumentNullException();

            _allowedToRun = true;
            _sourceUrl = source;
            _destination = destination;
            _chunkSize = chunkSizeInBytes;
            _contentLength = new Lazy<long>(GetContentLength);
            _progress = progress;

            if(!File.Exists(destination))
                BytesWritten = 0;
            else
            {
                try
                {
                    BytesWritten = new FileInfo(destination).Length;
                }
                catch
                {
                    BytesWritten = 0;
                }
            }
        }

        private long GetContentLength()
        {
            var request = (HttpWebRequest)WebRequest.Create(_sourceUrl);
            request.Method = "HEAD";

            using(var response = request.GetResponse())
                return response.ContentLength;
        }

        private async Task Start(long range)
        {
            if(!_allowedToRun)
                throw new InvalidOperationException();

            if(Done)
                //file has been found in folder destination and is already fully downloaded 
                return;

            var request = (HttpWebRequest)WebRequest.Create(_sourceUrl);
            request.UserAgent = MainWindow.userAgent;
            request.AddRange(range);

            using(var response = await request.GetResponseAsync())
            {
                using(var responseStream = response.GetResponseStream())
                {
                    using(var fs = new FileStream(_destination, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        while(_allowedToRun)
                        {
                            var buffer = new byte[_chunkSize];
                            var bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                            if(bytesRead == 0) break;

                            await fs.WriteAsync(buffer, 0, bytesRead);
                            BytesWritten += bytesRead;
                            _progress?.Report((double)BytesWritten / ContentLength);
                        }

                        await fs.FlushAsync();
                    }
                }
            }
        }

        public Task Start()
        {
            _allowedToRun = true;
            return Start(BytesWritten);
        }

        public void Pause()
        {
            _allowedToRun = false;
        }
    }

    // https://stackoverflow.com/a/12879118/7570821
    public class TimedWebClient : WebClient
    {
        // Timeout in milliseconds, default = 600,000 msec
        public int Timeout {get; set;}

        public TimedWebClient()
        {
            this.Timeout = 600000;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var objWebRequest = base.GetWebRequest(address);
            objWebRequest.Timeout = this.Timeout;
            return objWebRequest;
        }
    }

    // https://github.com/scottrippey/Progression/blob/master/Progression/Extras/ETACalculator.cs
    public interface IETACalculator
    {
        /// <summary> Clears all collected data.
        /// </summary>
        void Reset();

        /// <summary> Updates the current progress.
        /// </summary>
        /// <param name="progress">The current level of completion.
        /// Must be between 0.0 and 1.0 (inclusively).</param>
        void Update(float progress);

        /// <summary> Returns True when there is enough data to calculate the ETA.
        /// Returns False if the ETA is still calculating.
        /// </summary>
        bool ETAIsAvailable {get;}

        /// <summary> Calculates the Estimated Time of Arrival (Completion)
        /// </summary>
        DateTime ETA {get;}

        /// <summary> Calculates the Estimated Time Remaining.
        /// </summary>
        TimeSpan ETR {get;}
    }

    /// <summary> Calculates the "Estimated Time of Arrival"
    /// (or more accurately, "Estimated Time of Completion"),
    /// based on a "rolling average" of progress over time.
    /// </summary>
    public class ETACalculator : IETACalculator
    {
        /// <summary>
        /// </summary>
        /// <param name="minimumData">
        /// The minimum number of data points required before ETA can be calculated.
        /// </param>
        /// <param name="maximumDuration">
        /// Determines how many seconds of data will be used to calculate the ETA.
        /// </param>
        public ETACalculator(int minimumData, double maximumDuration)
        {
            this.minimumData = minimumData;
            this.maximumTicks = (long)(maximumDuration * Stopwatch.Frequency);
            this.queue = new Queue<ProgressItem>(minimumData * 2);
            this.timer = Stopwatch.StartNew();
        }

        private int minimumData;
        private long maximumTicks;
        private readonly Stopwatch timer;
        private readonly Queue<ProgressItem> queue;

        private ProgressItem current;
        private ProgressItem oldest;

        public void Reset()
        {
            queue.Clear();

            timer.Reset();
            timer.Start();
        }

        private void ClearExpired()
        {
            var expired = timer.ElapsedTicks - this.maximumTicks;
            while(queue.Count > this.minimumData && queue.Peek().Key < expired)
            {
                this.oldest = queue.Dequeue();
            }
        }

        /// <summary> Adds the current progress to the calculation of ETA.
        /// </summary>
        /// <param name="progress">The current level of completion.
        /// Must be between 0.0 and 1.0 (inclusively).</param>
        public void Update(float progress)
        {
            // If progress hasn't changed, ignore:
            if(this.current.Value == progress)
            {
                return;
            }

            // Clear space for this item:
            ClearExpired();

            // Queue this item:
            long currentTicks = timer.ElapsedTicks;
            this.current = new ProgressItem(currentTicks, progress);
            this.queue.Enqueue(this.current);

            // See if its the first item:
            if(this.queue.Count == 1)
            {
                this.oldest = this.current;
            }
        }

        /// <summary> Calculates the Estimated Time Remaining
        /// </summary>
        public TimeSpan ETR
        {
            get
            {
                // Create local copies of the oldest & current,
                // so that another thread can update them without locking:
                var oldest = this.oldest;
                var current = this.current;

                // Make sure we have enough items:
                if(queue.Count < this.minimumData || oldest.Value == current.Value)
                {
                    return TimeSpan.MaxValue;
                }

                // Calculate the estimated finished time:
                double finishedInTicks = (1.0d - current.Value) * (current.Key - oldest.Key) / (current.Value - oldest.Value);

                return TimeSpan.FromSeconds(finishedInTicks / Stopwatch.Frequency);
            }
        }

        /// <summary> Calculates the Estimated Time of Arrival (Completion)
        /// </summary>
        public DateTime ETA
        {
            get
            {
                return DateTime.Now.Add(ETR);
            }
        }

        /// <summary> Returns True when there is enough data to calculate the ETA.
        /// Returns False if the ETA is still calculating.
        /// </summary>
        public bool ETAIsAvailable
        {
            get
            {
                // Make sure we have enough items:
                return (queue.Count >= this.minimumData && oldest.Value != current.Value);
            }
        }
    }

    #if DEBUG
    // https://stackoverflow.com/a/48864902/7570821
    static class WinConsole
    {
        static public void Initialize(bool alwaysCreateNewConsole = true)
        {
            bool consoleAttached = true;
            if (alwaysCreateNewConsole
                || (AttachConsole(ATTACH_PARRENT) == 0
                && System.Runtime.InteropServices.Marshal.GetLastWin32Error() != ERROR_ACCESS_DENIED))
            {
                consoleAttached = AllocConsole() != 0;
            }

            if (consoleAttached)
            {
                InitializeOutStream();
                InitializeInStream();
            }
        }

        private static void InitializeOutStream()
        {
            var fs = CreateFileStream("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, FileAccess.Write);
            if (fs != null)
            {
                var writer = new StreamWriter(fs) { AutoFlush = true };
                Console.SetOut(writer);
                Console.SetError(writer);
            }
        }

        private static void InitializeInStream()
        {
            var fs = CreateFileStream("CONIN$", GENERIC_READ, FILE_SHARE_READ, FileAccess.Read);
            if (fs != null)
            {
                Console.SetIn(new StreamReader(fs));
            }
        }

        private static FileStream CreateFileStream(string name, uint win32DesiredAccess, uint win32ShareMode, FileAccess dotNetFileAccess)
        {
            var file = new Microsoft.Win32.SafeHandles.SafeFileHandle(CreateFileW(name, win32DesiredAccess, win32ShareMode, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero), true);
            if (!file.IsInvalid)
            {
                var fs = new FileStream(file, dotNetFileAccess);
                return fs;
            }
            return null;
        }

        #region Win API Functions and Constants
        [System.Runtime.InteropServices.DllImport("kernel32.dll",
            EntryPoint = "AllocConsole",
            SetLastError = true,
            CharSet = System.Runtime.InteropServices.CharSet.Auto,
            CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [System.Runtime.InteropServices.DllImport("kernel32.dll",
            EntryPoint = "AttachConsole",
            SetLastError = true,
            CharSet = System.Runtime.InteropServices.CharSet.Auto,
            CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        private static extern UInt32 AttachConsole(UInt32 dwProcessId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll",
            EntryPoint = "CreateFileW",
            SetLastError = true,
            CharSet = System.Runtime.InteropServices.CharSet.Auto,
            CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        private static extern IntPtr CreateFileW
        (
                string lpFileName,
                UInt32 dwDesiredAccess,
                UInt32 dwShareMode,
                IntPtr lpSecurityAttributes,
                UInt32 dwCreationDisposition,
                UInt32 dwFlagsAndAttributes,
                IntPtr hTemplateFile
        );

        private const UInt32 GENERIC_WRITE = 0x40000000;
        private const UInt32 GENERIC_READ = 0x80000000;
        private const UInt32 FILE_SHARE_READ = 0x00000001;
        private const UInt32 FILE_SHARE_WRITE = 0x00000002;
        private const UInt32 OPEN_EXISTING = 0x00000003;
        private const UInt32 FILE_ATTRIBUTE_NORMAL = 0x80;
        private const UInt32 ERROR_ACCESS_DENIED = 5;

        private const UInt32 ATTACH_PARRENT = 0xFFFFFFFF;

        #endregion
    }
    #endif
}
