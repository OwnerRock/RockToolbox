foreach ($a in @($Apps | Sort-Object { $_.Cat }, { $_.Name })) {
    $p = New-Object RockPkg
    $p.Name = $a.Name; $p.Id = $a.Id; $p.Category = $a.Cat
    $script:AllPkgs.Add($p)
}
