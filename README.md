![](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/raw/master/Assets/Images/GitHubREADME.webp)
> Screenshot taken during Version 6.7, the background image changes as new versions get released

# Better HI3 Launcher
A much better Honkai Impact 3rd launcher. Here are the key points:
* Very portable, the launcher is just one file (updates automatically too!)
* Supports all game servers (Global, SEA, CN, TW/HK/MO, KR and JP), can install and switch between them freely
  * **Please note that Steam version is not supported**
  * Epic Games version is technically supported, however to make purchases via Epic Games you have to launch the game using Epic Games Launcher
* Uses parallel downloading mechanism: Instead of downloading the game as one continuous stream of data it is now divided to several chunks, which dramatically speeds up the download process
* Has mirror support so you have more means of getting the game
* Includes other useful features, such as:
  * Downloading game cache - helps you get through the annoying "Updating settings" step
  * Repairing game files - uses a better method compared to the official launcher
  * Moving game files - the official launcher will still see the game
  * Better options for uninstallation, allowing to free more disk space
  * Setting custom resolution, custom FPS cap, resetting download type (so you can reselect which assets to download)
  * Customizable background - still images, GIFs, and even videos are supported!

## Getting the launcher
Go to the [releases page](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/releases/latest) and click on `BetterHI3Launcher.exe`.

You can save and use it wherever you want, it's that simple!

## Compatibility
Of course, a 64-bit version of Windows is required, since the game also requires it.

The launcher is confirmed to work on Windows 10 and Windows 11, but I personally develop and test on Windows 10.

Windows 8.1 and 7 should technically work if .NET Framework 4.6.2 or newer is installed, however they are no longer supported by Microsoft so I will not be accepting bug reports from those Windows versions.

## Is this safe?
I started this project to help fellow Captains have a better experience with the game. Since the release in January of 2021 there have been no cases of banned accounts.
After all, this is just an app that downloads the game and has some helpful utilities. Though it's probably a good idea to keep in mind that this project is not affiliated with HoYoverse in any way.

Ultimately it is for you to decide whether to trust me or not. The launcher's code is open source, so you're more than welcome to examine it. On that note...

## How can I contribute? 
Great question! This project is mainly supported by me alone, so I'd love to hear your feedback. You can [open an issue](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new/choose) to report a bug/suggest something or you can directly contribute with [pull requests](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/pulls).

> [!NOTE]
> * Set the merge branch to `dev` when creating a PR
> * The project is made with Visual Studio 2019, so there could be problems on a different version of Visual Studio
>   * That said, if you do use a different version then you should not include any project changes in your PR

You can also contribute by translating the launcher into a language you speak. **Please only contribute languages the launcher doesn't yet have! Check the issues for existing contributions.**

Here's how to do it:
1. [Download English source text](https://bpnet.work/bh3?launcher_translations=get_contents_en)
2. You will see a bunch of lines with text, on each line there are two pieces of text: you don't need to touch the one on the left, only translate the one on the right (after the space). Please try your best not to touch special characters such as `{0}`, `\n`, or `\"`, also make sure to not add any unnecessary spaces, newlines or other characters.
3. After you're done, [create an issue](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new?assignees=BuIlDaLiBlE&labels=language+contribution&template=language_contribution.md&title=Language+contribution+%5BNAME+OF+THE+LANGUAGE+HERE%5D) with the name of the language and attach the file with translated text to it.

That's it, you're done! Please be ready to answer if there happens to be something that I need to clarify about your translation.

After I verify your submission and implement it I will list your name in the "About" page of the launcher as a token of gratitude. (´｡• ᵕ •｡`)

Translations may need updating in the future as I keep adding and changing things so it would be a huge help if you subscribe to the issue so that you can receive notifications whenever I will be needing more help (won't be very often, I promise!). Thank you in advance!