# Module: features/Common - Shared types (RockClean, RockStartup) and Format-Bytes
# Dot-sourced by RockToolbox.ps1 (shares its script scope). Do not run this file on its own.

# ------------------------------------------------------------------
# Rock additions: safety restore point, global Stop, backup / import of selections,
# System Info, Cleanup, Startup Manager, Repair, Pause updates
# ------------------------------------------------------------------
Add-Type -TypeDefinition @'
using System.ComponentModel;
public class RockClean : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Id { get; set; }
    public string Name { get; set; }
    public string Desc { get; set; }
    public string Size { get; set; }
    public long Bytes { get; set; }
    public bool Recommended { get; set; }
}
public class RockStartup : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    private string _status;
    public string Status {
        get { return _status; }
        set { _status = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Status")); }
    }
    public string Name { get; set; }
    public string Kind { get; set; }
    public string Group { get; set; }
    public string Command { get; set; }
    public string Source { get; set; }
    public string Ref { get; set; }
}
'@

function Format-Bytes {
    param([double]$b)
    if ($b -ge 1TB) { return ('{0:0.0} TB' -f ($b / 1TB)) }
    if ($b -ge 1GB) { return ('{0:0.0} GB' -f ($b / 1GB)) }
    if ($b -ge 1MB) { return ('{0:0} MB' -f ($b / 1MB)) }
    if ($b -ge 1KB) { return ('{0:0} KB' -f ($b / 1KB)) }
    return ('{0:0} B' -f $b)
}

