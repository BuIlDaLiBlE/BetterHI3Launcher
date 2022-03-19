using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterHI3Launcher
{
	public enum LogAdapterType { Default = 0, Error = 1, Warning = 2}
	internal static class LogAdapter
	{
		static LogInvoker Invoker = new LogInvoker();
		public static void WriteLog(string Text, bool NewLine = true, LogAdapterType LogType = LogAdapterType.Default) => Invoker.WriteLog(Text, NewLine, LogType);
	}

	internal class LogInvoker
	{
		public static event EventHandler<LogProperties> LogEvent;
		public void WriteLog(string Text, bool NewLine, LogAdapterType LogType) => LogEvent?.Invoke(this, new LogProperties(Text, NewLine, (int)LogType));
	}

	public class LogProperties
	{
		public LogProperties(string Text, bool NewLine, int LogType)
		{
			this.Text = Text;
			this.NewLine = NewLine;
			this.LogType = LogType;
		}
		public int LogType { get; private set; }
		public string Text { get; private set; }
		public bool NewLine { get; private set; }
	}
}
