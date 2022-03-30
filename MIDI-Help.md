## Default Drum Game Mapping
If you have any trouble with triggering, you will want to verify that your drum module is outputing a MIDI value that maps to the correct channel according to the following list. If your output values differ, you will need to configure either Drum Game or your drum module (see [here](#fixing-mapping-issues) for help). To examine the current mappings, you can either go to `F1 > View MIDI` or `Settings > MIDI Mapping > Edit`.
* 35 => BassDrum
* 36 => BassDrum
* 37 => SideStick
* 39 => SideStick
* 38 => Snare
* 48 => SmallTom
* 45 => MediumTom
* 41 => LargeTom
* 43 => LargeTom
* 40 => Rim
* 58 => Rim
* 47 => Rim
* 50 => Rim
* 42 => ClosedHiHat
* 22 => ClosedHiHat
* 46 => OpenHiHat
* 26 => OpenHiHat
* 54 => OpenHiHat
* 44 => HiHatPedal
* 51 => Ride
* 53 => Ride
* 49 => Crash
* 57 => Crash
* 59 => Crash
* 55 => Splash
* 52 => China

This list is based off of the [default values for the TD-27](https://rolandus.zendesk.com/hc/en-us/articles/4407474950811-TD-27-Default-MIDI-Note-Map).

## Fixing mapping issues
All you have to do to adjust mapping issues is go to settings (<kbd>Ctrl</kbd>+<kbd>,</kbd>) and click `Edit` on `MIDI Mapping`. From there you can just hit the triggers that you want to adjust.

Alternatively you can change the MIDI notes on your drum module to match the default mapping. In some cases this may be required if the defaults for your module have overlap. For example, on the Alesis Crimson II, open/closed hi-hat map to the same MIDI note. If you don't want to or can't change the note mapping on your module, you can also use the `Channel Equivalents` setting to allow open/closed hi-hat to trigger eachother. To do this, just add `Closed Hi Hat => Open Hi Hat` and `Open Hi Hat => Closed Hi Hat`

### Changing MIDI notes on the Alesis Crimson II
1. Press the Menu button on your module, then move down to Trigger and press Enter to enter the Trigger Settings menu.
2. Use the knob to change the selected trigger to the desired drum/cymbal.
3. Press the Down button and select MIDI Note.
4. Use the dial to change the assigned MIDI note.

[See also](https://www.alesis.com/kb/article/2517)
