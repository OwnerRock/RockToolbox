# ------------------------------------------------------------------
Add-Type -TypeDefinition @'
using System.ComponentModel;
public class RockPkg {
    public string Name { get; set; }
    public string Id { get; set; }
    public string Category { get; set; }
}
public class RockGod {
    public string Name { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public int Index { get; set; }
}
public class RockUpg : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Name { get; set; }
    public string Id { get; set; }
    public string Version { get; set; }
    public string Available { get; set; }
    public string Source { get; set; }
}
public class RockWU : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Title { get; set; }
    public string KB { get; set; }
    public string Size { get; set; }
    public string Type { get; set; }
    public string Category { get; set; }
    public string Id { get; set; }
}
public class RockInst : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Name { get; set; }
    public string Version { get; set; }
    public string Publisher { get; set; }
    public string Source { get; set; }
    public string Size { get; set; }
    public string Key { get; set; }
    public string Cmd { get; set; }
    public string Quiet { get; set; }
}
public class RockDrv : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    private bool _sel;
    public bool Selected {
        get { return _sel; }
        set { _sel = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); }
    }
    public string Title { get; set; }
    public string DrvClass { get; set; }
    public string Kind { get; set; }
    public string Provider { get; set; }
    public string Version { get; set; }
    public string DrvDate { get; set; }
    public string Size { get; set; }
    public string Id { get; set; }
}
'@

# ---------------- Winget ID: ready list ----------------
$script:AllPkgs = New-Object 'System.Collections.Generic.List[RockPkg]'
