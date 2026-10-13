if ([bool]$script:Settings['RememberTab'] -and $pages.Contains([string]$script:Settings['LastTab'])) { $startPage = [string]$script:Settings['LastTab'] }
elseif ($pages.Contains([string]$script:Settings['StartTab'])) { $startPage = [string]$script:Settings['StartTab'] }
