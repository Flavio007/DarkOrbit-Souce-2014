param([string]$CatalogPath = (Join-Path $PSScriptRoot '..\config\quests.xml'))
$ErrorActionPreference = 'Stop'
[xml]$catalog = Get-Content -LiteralPath $CatalogPath -Raw -Encoding UTF8
$quests = @($catalog.quests.quest)
if (@($quests | Group-Object id | Where-Object Count -gt 1).Count) { throw 'Duplicate quest IDs' }
$reference = Join-Path $PSScriptRoot '..\config\reference'
$inventory = Get-Content (Join-Path $reference 'mission-list-63014.json') -Raw -Encoding UTF8 | ConvertFrom-Json
function Normalized([string]$title) { return ($title -replace '^Quest/', '' -replace '[^a-zA-Z0-9]', '').ToLowerInvariant() }
$events = @('killNpc','killNpcs','killPlayers','collectOre','collectCargoOre','collectCargoBox','collectBonusBox','sellOre','damage','damageNpc','damagePlayers','damageTaken','travel','jump','visitMap','visitPosition','visitPortal','visitHomeBase','stayOnMap','elapsed','radiation','stealCargoBox','visitMissionControl','joinClan')
foreach ($quest in $quests | Where-Object sourceRevision -eq '63014') {
    $name = Normalized $quest.title
    if (!@($inventory.missions | Where-Object { $_.level -eq [int]$quest.minLevel -and (Normalized $_.title) -eq $name }).Count) { throw "Quest outside saved revision: $($quest.id)" }
    if (!@($quest.conditions.condition).Count -or !@($quest.rewards.reward).Count) { throw "Empty quest: $($quest.id)" }
    foreach ($condition in $quest.conditions.condition) {
        if ($events -notcontains $condition.type -or [int]$condition.target -lt 1) { throw "Invalid objective: $($quest.id)" }
        if ([int]$quest.factionId -gt 0 -and [int]$condition.targetFactionId -eq [int]$quest.factionId) { throw "Quest targets its own company: $($quest.id)" }
        if ([int]$quest.factionId -gt 0 -and [int]$condition.mapOwner -eq [int]$quest.factionId -and $name -match '^(steal|foreignoccupation)') { throw "Enemy territory is own territory: $($quest.id)" }
    }
}
$first = $quests | Where-Object id -eq '1001'
if ([int]$first.conditions.condition[0].target -ne 8 -or [int]$first.conditions.condition[1].target -ne 6) { throw 'First Assignment objectives changed' }
$hunter = @($quests | Where-Object title -eq 'Hunter or gatherer')
if ($hunter.Count -ne 1 -or @($hunter[0].conditions.condition | Group-Object branch).Count -ne 2) { throw 'Hunter/gatherer branches missing' }
$pirate = @($quests | Where-Object { $_.title -match '^(Top of the food chain|Undesirables|Speed|Orbital explosives expert|Little monsters) \([1-4]\)$' })
if ($pirate.Count -ne 20 -or @($pirate | Where-Object minLevel -ne '12').Count) { throw 'Pirate quest variants/levels changed' }
$daily = @($quests | Where-Object daily -eq '1')
if ($daily.Count -ne 10) { throw 'Captured daily mission count changed' }
foreach ($row in $inventory.missions) {
    foreach ($id in $row.questIds) {
        if (!@($quests | Where-Object id -eq ([string]$id)).Count) { throw "Inventory references missing quest $id" }
    }
}
Write-Output "Classic catalog passed: unique IDs, saved-revision allowlist, objectives, faction targets, 20 pirate variants, 10 dailies and coverage references."
