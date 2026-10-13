$btnTweakRec.Add_Click({ foreach ($cb in $tweakBoxes) { $cb.IsChecked = [bool]$cb.Tag.Rec } })
