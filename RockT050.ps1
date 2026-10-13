function Update-SystemCursors {
    # SPI_SETCURSORS (0x57) + SPIF_UPDATEINIFILE|SPIF_SENDCHANGE (3): reload cursors without logging off
    [void][RockNative.Cur]::SystemParametersInfo(0x57, 0, [IntPtr]::Zero, 3)
}
