﻿![](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/raw/master/.github/GitHubREADME.webp)
> Screenshot taken during Version 6.7, background image changes with each new major version of the game

# Better HI3 Launcher
A much better Honkai Impact 3rd launcher. Here are the key points:
* Very portable, the launcher is just one file (updates automatically too!)
* Supports all game servers (Global, SEA, CN, TW/HK/MO, KR and JP), can install and switch between them freely
  * **Please note that Steam version is not supported**
  * Epic Games version is technically supported, however to make purchases via Epic Games you have to launch the game using Epic Games Launcher
* Uses parallel downloading mechanism: Instead of downloading the game as one continuous stream of data it is now divided to several chunks, which really speeds up the download process
* Has mirror server support so you have alternaive sources of getting the game
* Other various useful features:
  * Downloading game cache - helps you get through the "Updating settings" step in-game
  * Repairing game files - uses a slightly different method compared to the official launcher which may provide a better help
  * Moving game files - the official launcher will still recognize the game
  * Better options for uninstallation, allowing to free more disk space
  * Setting custom resolution, custom FPS cap, resetting download type so you can reselect which asset type to download (HD/Full)
  * Customizable background - still images, GIFs, and even videos are supported!

## Getting the launcher
Go to the [releases page](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/releases/latest) and click on `BetterHI3Launcher.exe`.

You can save and use it wherever you want, it's that simple!

## Compatibility
Of course, a 64-bit version of Windows is required since the game also requires it. The launcher is tested to work on modern versions of Windows (10 and 11).

Windows 8.1 and 7 should technically work if .NET Framework 4.6.2 or newer is installed, however they are no longer supported by Microsoft so I may not accept bug reports from these versions of Windows.

## Is this safe?
I started this project to help fellow Captains have a better experience with the game. Since the release in January of 2021 there have been no cases of banned accounts.
After all, this is just an app that downloads the game and has some helpful utilities. Though it's probably a good idea to keep in mind that this project is not affiliated with HoYoverse in any way and they reserve the final say.

Ultimately it is for you to decide whether to trust the project or not. The launcher's code is open source, so you're more than welcome to examine it. On that note...

## How can I contribute? 
Great question! This project is mainly supported by the main developer (@BuIlDaLiBlE) alone, so I'd love to hear your feedback. You can [open an issue](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new/choose) to report a bug/suggest something or you can directly contribute with [pull requests](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/pulls).

> [!NOTE]
> * Set the merge branch to `dev` when creating a PR

You can also contribute by translating the launcher into a language you speak. **Please only contribute languages the launcher doesn't yet have! Check the issues for existing contributions.**

Here's how to do it:
1. [Download English source text](https://bpnet.work/bh3?launcher_translations=get_contents_en)
2. You will see a bunch of lines with text, on each line there are two pieces of text: you don't need to touch the one on the left, only translate the one on the right (after the space). Please try your best not to touch special characters such as `{0}`, `\n`, or `\"`, also make sure to not add any unnecessary spaces, newlines or other characters.
3. After you're done, [create an issue](https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new?assignees=BuIlDaLiBlE&labels=language+contribution&template=language_contribution.md&title=Language+contribution+%5BNAME+OF+THE+LANGUAGE+HERE%5D) with the name of the language and attach the file with translated text to it.

That's it, you're done! Please be ready to answer if there happens to be something that I need to clarify about your translation.

After I verify your submission and implement it I will list your name in the "About" page of the launcher as a token of gratitude. (´｡• ᵕ •｡`)

Translations may need updating in the future as I keep adding and changing things so it would be a huge help if you subscribe to the issue so that you can receive notifications whenever I will be needing more help (won't be very often, I promise!). Thank you in advance!