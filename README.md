# Better HI3 Launcher
A much better Honkai Impact 3rd launcher. Here are the key points:
* No need to install, it is just one file
* Supports all game clients (Global, SEA, CN, TW/HK/MO and KR), can switch between them freely
  * **Please note that Steam version is not supported**
* Uses parallel downloading mechanism: Instead of downloading the game as one continuous stream of data it is now divided to several chunks, which dramatically speeds up the download process
* Has mirror support so you have more download sources to choose from
* Automatically updates itself, so you are always getting the latest and greatest!
* Has lots of useful features, such as:
  * Downloading game cache (fixes the problems that occur during "Updating settings")
  * Repairing game files (only the files that are needed will be downloaded)
  * Moving game files (properly, so that the original launcher still works)
  * Setting custom resolution, custom FPS, resetting download type (to reselect assets download option)
  * Customizable background (supports still images, GIFs, and even videos!)

## Getting the launcher
Go to the [releases page](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/releases/latest) and click on `BetterHI3Launcher.exe`.

You can save and use it wherever you want, it's that simple!

## Compatibility
First of all, a 64-bit version of Windows is required, since the game also requires it.

Confirmed to work on Windows 10 and Windows 11, but the machine I develop on runs Windows 10.

Windows 8.1 should work if .NET Framework 4.6.1 or newer is installed.

Windows 7 should technically work too if the above condition is met, however it's not supported by Microsoft since January of 2020 so I may not provide much support.

## Is this safe?
I started this project with hope to help fellow Captains have a better experience with the game. Since the release in January of 2021 there have been no cases of bans.
After all, this is just an app that downloads the game and has some helpful utilities. However, it is wise to remember that this project is not affiliated with HoYoverse and thus they have the final say about it.

Ultimately, it is for you to decide whether to trust me or not. The launcher's code is open source though, so you're always welcome to look at it. On that note...

## How can I contribute? 
Great question! This project is actively supported, you are always welcome to [open an issue](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new/choose) (report a bug or suggest something) or directly contribute to the project via [pull requests](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/pulls).

You can also contribute by translating the launcher into a language you speak. **Please only contribute languages the launcher doesn't yet have!**

Here's how to do it:
1. [Download English source text](https://bpnet.work/bh3?launcher_translations=get_contents_en)
2. You will see a bunch of lines with text, on each line there are two pieces of text: you don't need to touch the one on the left, only translate the one on the right (after the space). Please try your best not to touch special characters such as `{0}`, `\n`, or `\"`, don't add any unnecessary spaces, newlines or other characters.
3. After you're done, [create an issue](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new?assignees=BuIlDaLiBlE&labels=language+contribution&template=language_contribution.md&title=Language+contribution+%5BNAME+OF+THE+LANGUAGE+HERE%5D) with the name of the language and attach the file with translated strings to it.

That's it, you're done! Please be ready to answer if there happens to be something that I need to clarify about your translation.

After I verify your submission and implement it you can be sure I will list your name in the "About" section of the launcher.

Translations may need updating in the future as I keep adding and changing things so I'd be eternally grateful if you keep being subscribed to the issue so that you receive notifications whenever I need more help.