$btnDebRec.Add_Click({ foreach ($cb in $bloatBoxes) { $cb.IsChecked = [bool]$cb.Tag.Rec } })
