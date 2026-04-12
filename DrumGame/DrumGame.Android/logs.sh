LOG_DIR="/sdcard/Android/data/tk.jumprocks.drumgame/files/logs"
X=1

adb shell "cat $LOG_DIR/\$(ls -t1 $LOG_DIR | sed -n '${X}p')"