# Android
Android support is very new and is only lightly tested. I have it working on my phone, but there are thousands of other devices out there that may or may not work. Please report any issues on GitHub or Discord so I can get them fixed.

## Setup Instructions
- Download the `.apk` from the latest release https://github.com/Jumprocks1/drum-game/releases
- You should be able to install it from your mobile browser, it may ask about "installing from unverified sources"
- It might pop up with a Google Play Protect scan, feel free to scan if desired
- Once installed, open it, it should say `Failed to locate folder: resources.`
- Plug your Android device into a PC with the game already downloaded - we have to copy the resources folder over
- In Android, set the USB connection to "File Transfer"
- On your PC file explorer, navigate to Android Phone/Tablet > Internal shared storage > Android > data > tk.jumprocks.drumgame > files
- Go to your desktop copy of Drum Game and copy the resources folder into that Android `files` folder
    - If you're concerned about storage on your Android device, you probably only need:
    - `resources/fonts` - required
    - `resources/maps` - only need the maps you want to play
    - `resources/skins` - if you're not using the default skin
- At this point you should be able to force close DrumGame on Android and reopen it successfully
- If you run into any crashes, the logs will be in that `files` folder mentioned above

## Hardware
If you want the full experience, I recommend using a 3.5mm male-to-male cable to output the phone's audio into your drum module. You'll also need a way to plug the MIDI output of your drum module into the phone for scoring/controls.

I test with something like this https://www.amazon.com/Adapter-Anker-High-Speed-Transfer-Notebook/dp/B08HZ6PS61. A USB B to C cable like this should also work if it matches your module https://www.amazon.com/UGREEN-Printer-Braided-Compatible-Printers/dp/B086ZGYK6K.

## Quirks
- You have to use the back button on your device to exit maps part way through. This usually requires swiping one of the sides of the device first
- Maps will be annoying to transfer over to the mobile device. It should be possible for me to make it so the game open zip files.

## Useful settings
- Draw Scale - zooms in the entire game if it feels too small
- MIDI Mapping - press the music note in the top right to configure MIDI input device
- Cursor Inset - if you're zoomed in a lot, you can decrease this to move the (notation) cursor closer to the left side of the screen

## Adding maps
Bit awkward currently, but there are 3 options:
1. Copy files to a subfolder inside `resources`. On Android, click the folder button in the bottom center > Add new library. Folder Path = subfolder name. Enable scanning for desired formats.
2. Close the game on Android. Copy drumgame.ini off the Android folder. Edit and set WatchImportFolder = True, should be around line 30. Copy back to Android. Create `resources/import`. Place .zip files in the import folder. When you boot the Android game, it should load all files in that folder.
3. Export maps on desktop using `F1 > Export Map`. Extract the resultant .zip into the `resources/maps` folder on Android.

## Source code
I haven't uploaded the source for the .apk yet since I've been messing with it locally, I'll get it in the main branch soon.