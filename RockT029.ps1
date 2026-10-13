$btnExpand.Add_Click({
    foreach ($sec in $script:Sections) { $sec.Wrap.Visibility = 'Visible'; $sec.Header.Text = '- ' + $sec.Cat }
})
