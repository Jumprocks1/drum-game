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

## Quirks
- You have to use the back button on your device to exit maps part way through. This usually requires swiping one of the sides of the device first
- Maps will be annoying to transfer over to the mobile device. It should be possible for me to make it so the game open zip files.

## Useful settings
- Draw Scale - zooms in the entire game if it feels too small
- MIDI Mapping - press the music note in the top right to configure MIDI input device
- Cursor Inset - if you're zoomed in a lot, you can decrease this to move the (notation) cursor closer to the left side of the screen