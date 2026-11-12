param(
    [Parameter(Mandatory = $true)] [string] $PlayerPath,
    [Parameter(Mandatory = $true)] [string] $OutputDirectory,
    [Parameter(Mandatory = $true)] [int] $Port,
    [ValidateSet('Review','Authority','Session','Departure','Spectator','Budget','UI','Presentation','Relay')] [string] $Scenario = 'Review',
    [switch] $Render,
    [int] $Width = 1280,
    [int] $Height = 720
)

$ErrorActionPreference = 'Stop'
$playerExe = (Resolve-Path -LiteralPath $PlayerPath).Path
$runDirectory = (New-Item -ItemType Directory -Path $OutputDirectory -ErrorAction Stop).FullName
$script:players = @{}
$script:message = 0
$hostPeer = 'ValidationHost'
$client1 = 'ValidationClient1'
$client2 = 'ValidationClient2'
$latePeer = 'ValidationClient3'
$primaryPeers = @($hostPeer,$client1,$client2)

function Confirm-Check($condition,[string]$description) {
    if (!$condition) { throw "Assertion failed: $description" }
}

function Start-Peer([string]$peer) {
    $playerLog = Join-Path $runDirectory "$peer.log"
    $display = if ($Render -and ($Scenario -ne 'Presentation' -or $peer -eq $client1)) {
        "-force-d3d11 -screen-fullscreen 0 -screen-width $Width -screen-height $Height"
    } else { '-batchmode -nographics' }
    $arguments = '{0} -logFile "{1}" -phase2Driver "{2}" {3}' -f $display,$playerLog,$runDirectory,$peer
    if ($Scenario -eq 'Relay') { $arguments += ' -phase2Relay' }
    $process = Start-Process -FilePath $playerExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $script:players.Add($peer,$process)
    $ready = Join-Path $runDirectory "$peer.ready"
    $deadline = [DateTime]::UtcNow.AddSeconds(80)
    while (!(Test-Path -LiteralPath $ready)) {
        if ($process.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw "$peer failed to start" }
        Start-Sleep -Milliseconds 80
    }
}

function Invoke-Peer([string]$peer,[string]$command) {
    $id = ++$script:message
    $request = Join-Path $runDirectory "$peer.request"
    [IO.File]::WriteAllText("$request.tmp","$id $command")
    [IO.File]::Move("$request.tmp",$request)
    $response = Join-Path $runDirectory "$peer.$id.response"
    $deadline = [DateTime]::UtcNow.AddSeconds($(if ($command -like 'capture *' -or $command -eq 'ui-menu-qa') { 30 } else { 8 }))
    while (!(Test-Path -LiteralPath $response)) {
        if ($script:players[$peer].HasExited -or [DateTime]::UtcNow -gt $deadline) { throw "$peer failed to answer $command" }
        Start-Sleep -Milliseconds 15
    }
    while ($true) {
        try {
            $body = [IO.File]::ReadAllText($response)
            break
        } catch {
            if ($_.Exception.InnerException -isnot [IO.IOException] -or [DateTime]::UtcNow -gt $deadline) { throw }
            Start-Sleep -Milliseconds 25
        }
    }
    if ($body.StartsWith('ERROR:')) { throw "$peer executing $command - $body" }
    if ($command -eq 'state') { return ConvertFrom-Json $body }
    if ($command -eq 'schedule') { return $body.Split(',') }
    Confirm-Check ($body -eq 'OK') "$peer returned an unexpected result for $command"
}

function Wait-State([string]$peer,[scriptblock]$predicate,[string]$description,[int]$seconds = 12) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        $state = Invoke-Peer $peer 'state'
        if (& $predicate $state) { return $state }
        Start-Sleep -Milliseconds 55
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "$peer timed out waiting for $description. Last state: $($state | ConvertTo-Json -Depth 6 -Compress)"
}

function Join-Peer([string]$peer,[int]$connectionPort) {
    Invoke-Peer $peer "join $connectionPort"
    $null = Wait-State $peer { param($s) $s.connected -and $s.lobbyVisible -and $s.phase -eq 'Lobby' } 'connected lobby'
    Invoke-Peer $peer 'profile'
}

function Get-Owner($state) {
    $owner = @($state.players | Where-Object owner)
    Confirm-Check ($owner.Count -eq 1) "exactly one owner on $($state.localId)"
    return $owner[0]
}

function Receive-Acks([string]$peer,[int]$previous,[int]$count = 1) {
    $state = Wait-State $peer { param($s) $s.acks.Count -ge $previous + $count } "$count new action acknowledgments"
    return @($state.acks | Select-Object -Skip $previous)
}

function Check-Ack([string]$peer,[string]$command,[string]$kind,[bool]$accepted) {
    $previous = (Invoke-Peer $peer 'state').acks.Count
    Invoke-Peer $peer $command
    $ack = @(Receive-Acks $peer $previous)
    Confirm-Check ($ack.Count -eq 1 -and $ack[0].kind -eq $kind -and $ack[0].accepted -eq $accepted -and $ack[0].message) "$peer $command acknowledgment"
}

function Start-Room {
    Invoke-Peer $hostPeer "host $Port"
    $null = Wait-State $hostPeer { param($s) $s.connected -and $s.lobbyVisible -and $s.phase -eq 'Lobby' } 'hosting lobby'
    Invoke-Peer $hostPeer 'profile'
}

function Wait-Roster([string]$peer,[int]$count) {
    return Wait-State $peer { param($s) $s.players.Count -eq $count } "roster of $count"
}

function Check-PrivateViews([string[]]$peers) {
    $script:roles = @{}
    $script:byId = @{}
    $hostState = Invoke-Peer $hostPeer 'state'
    foreach ($peer in $peers) {
        $state = Wait-State $peer { param($s) $s.stamp -eq $hostState.stamp -and $s.phase -eq 'PlayerTurn' -and (Get-Owner $s).role } 'replicated turn and role'
        $local = Get-Owner $state
        $script:roles[$peer] = $local.role
        $script:byId[$state.localId] = $peer
        Confirm-Check ($local.skips -eq [Math]::Ceiling($peers.Count / 4.0) -and $state.score -eq 0 -and $state.moves -eq 15 -and $state.target -eq $hostState.target) "$peer owner budget and public match state"
        foreach ($remote in @($state.players | Where-Object { !$_.owner })) {
            Confirm-Check (!$remote.role -and $remote.diceCount -eq 0 -and $remote.block -eq 0 -and $remote.curse -eq 0 -and $remote.shift -eq 0 -and $remote.teammates -eq 0) "$peer cannot access another player's private view"
            if ($peer -ne $hostPeer) {
                Confirm-Check (!$remote.receivedSecret -and !$remote.receivedRoll) "$peer does not receive another player's underlying role or roll"
            }
        }
        if ($state.activeId -eq $state.localId) {
            Confirm-Check ($local.diceCount -eq 3 -and $local.rollStamp -eq $state.stamp) "$peer first roll received with current stamp"
        } else {
            Confirm-Check ($local.diceCount -eq 0) "$peer observing owner has no dice"
        }
    }
}



function Check-Authority {
    for ($attempt = 0; $attempt -lt 8; $attempt++) {
        Start-Room
        Join-Peer $client1 $Port
        Join-Peer $client2 $Port
        $null = Wait-Roster $hostPeer 3
        Invoke-Peer $hostPeer 'lengthen-turns'
        Invoke-Peer $hostPeer "seed $(1901 + $attempt)"
        Check-Ack $client1 'start' 'Start' $false
        Check-Ack $hostPeer 'start' 'Start' $true
        $first = Wait-State $hostPeer { param($s) $s.phase -eq 'PlayerTurn' } 'first turn'
        Check-PrivateViews $primaryPeers
        $activePeer = $script:byId[$first.activeId]
        if ($activePeer -ne $hostPeer -and $script:roles[$hostPeer] -ne 'Imposter') {
            break
        }
        Stop-Room $primaryPeers
    }
    Confirm-Check ($activePeer -ne $hostPeer -and $script:roles[$hostPeer] -ne 'Imposter') 'fixture produced remote first actor and remote imposter'
    'PASS: host-only start, synchronized public state, owner-only roles and first private roll on a remote client'
    $observer = @($primaryPeers | Where-Object { $_ -ne $activePeer })[0]
    $imposter = @($primaryPeers | Where-Object { $script:roles[$_] -eq 'Imposter' })[0]
    $normal = @($primaryPeers | Where-Object { $script:roles[$_] -eq 'Normal' })[0]
    foreach ($peer in $primaryPeers) { Invoke-Peer $peer 'cache first' }
    $firstDie = (Get-Owner (Invoke-Peer $activePeer 'state')).die
    $beforeObserver = (Invoke-Peer $activePeer 'state').acks.Count
    Check-Ack $observer "move $firstDie" 'Move' $false
    Confirm-Check ((Invoke-Peer $activePeer 'state').acks.Count -eq $beforeObserver) 'rejection not broadcast to active player'
    Check-Ack $hostPeer 'start' 'Start' $false
    Check-Ack $normal 'curse 0' 'CursedDice' $false
    Check-Ack $imposter 'curse 999999' 'CursedDice' $false
    $imposterBefore = Get-Owner (Invoke-Peer $imposter 'state')
    Confirm-Check ($imposterBefore.curse -eq 2) 'invalid target leaves owner charge intact'
    $target = (Invoke-Peer $imposter 'state').players | Where-Object { $_.id -ne $first.activeId } | Select-Object -First 1
    Check-Ack $imposter "curse $($target.id)" 'CursedDice' $true
    $null = Wait-State $imposter { param($s) (Get-Owner $s).curse -eq 1 } 'spent owner curse charge'
    Check-Ack $imposter "block $($target.id)" 'ForcedBlock' $false
    Confirm-Check ((Get-Owner (Invoke-Peer $imposter 'state')).block -eq 1) 'round cap did not spend block charge'
    $untouched = Invoke-Peer $hostPeer 'state'
    Confirm-Check ($untouched.score -eq 0 -and $untouched.moves -eq 15 -and $untouched.match -eq $first.match) 'invalid actions and mid-match start did not mutate score/moves/match'
    'PASS: out-of-turn rejection is caller-only; invalid role and target preserve charge; one accepted ability spends one owner charge; round cap enforced'

    $previous = (Invoke-Peer $activePeer 'state').acks.Count
    Invoke-Peer $activePeer "move-twice $firstDie"
    $acks = @(Receive-Acks $activePeer $previous 2)
    Confirm-Check ($acks.Count -eq 2 -and $acks[0].kind -eq 'Move' -and $acks[0].accepted -and !$acks[1].accepted) 'same-stamp duplicate accepted once'
    $second = Wait-State $hostPeer { param($s) $s.moves -eq 14 -and $s.action -ne $first.action } 'single committed move'
    Confirm-Check ($second.score -eq $firstDie) 'duplicate spent exactly one move and added one face'
    $secondPeer = $script:byId[$second.activeId]
    $next = Wait-State $secondPeer { param($s) (Get-Owner $s).diceCount -eq 3 -and $s.stamp -eq $second.stamp } 'next owner roll'
    Check-Ack $secondPeer "move $((Get-Owner $next).die) @first" 'Move' $false
    $afterStale = Invoke-Peer $hostPeer 'state'
    Confirm-Check ($afterStale.moves -eq 14 -and $afterStale.score -eq $firstDie) 'stale turn did not mutate score or move pool'
    'PASS: duplicate move consumed one die and one move; new active player cannot use previous turn stamp'
    $observer = @($primaryPeers | Where-Object { $_ -ne $hostPeer -and $_ -ne $secondPeer })[0]
    $observedBefore = @((Invoke-Peer $observer 'state').players | Where-Object id -eq $next.localId)[0].skips
    Check-Ack $secondPeer 'skip' 'Skip' $true
    $null = Wait-State $secondPeer { param($s) (Get-Owner $s).skips -eq 0 } 'spent owner skip'
    $observedAfter = @((Invoke-Peer $observer 'state').players | Where-Object id -eq $next.localId)[0].skips
    Confirm-Check ($observedAfter -eq $observedBefore -and (Invoke-Peer $hostPeer 'state').moves -eq 14) 'observer skip snapshot unchanged and shared moves unspent'
    'PASS: the owner sees their skip charge spend while an ordinary observer sees no budget update'

    for ($turn = 0; $turn -lt 3; $turn++) {
        $at = Invoke-Peer $hostPeer 'state'
        if ($at.phase -eq 'Discussion') { break }
        Confirm-Check ($at.phase -eq 'PlayerTurn') 'expected a turn before discussion'
        Invoke-Peer $hostPeer 'advance-timer'
        $null = Wait-State $hostPeer { param($s) $s.stamp -ne $at.stamp -or $s.phase -ne 'PlayerTurn' } 'turn to advance'
    }
    $null = Wait-State $hostPeer { param($s) $s.phase -eq 'Discussion' } 'discussion'
    Invoke-Peer $hostPeer 'advance-timer'
    $imposterId = (Invoke-Peer $imposter 'state').localId
    foreach ($peer in $primaryPeers) {
        $null = Wait-State $peer { param($s) $s.phase -eq 'Voting' } 'voting'
        Check-Ack $peer "vote $imposterId" 'Vote' $true
    }
    $elimination = Wait-State $hostPeer { param($s) $s.phase -in @('Elimination','MatchEnded') } 'elimination'
    if ($elimination.phase -eq 'Elimination') { Invoke-Peer $hostPeer 'advance-timer' }
    $end = Wait-State $hostPeer { param($s) $s.phase -eq 'MatchEnded' } 'terminal result'
    $null = Wait-State $imposter { param($s) $s.phase -eq 'MatchEnded' -and !(Get-Owner $s).alive } 'ejected owner and terminal state'
    Check-Ack $imposter 'block 0' 'ForcedBlock' $false
    Check-Ack $normal 'move 1' 'Move' $false
    Check-Ack $normal 'vote-skip' 'Vote' $false
    $stillEnded = Invoke-Peer $hostPeer 'state'
    Confirm-Check ($stillEnded.phase -eq 'MatchEnded' -and $stillEnded.stamp -eq $end.stamp -and $stillEnded.score -eq $end.score -and $stillEnded.moves -eq $end.moves) 'terminal mutations did not change the result'
    Check-Ack $client1 'rematch' 'Rematch' $false
    Check-Ack $hostPeer 'rematch' 'Rematch' $true
    $reset = Wait-State $hostPeer { param($s) $s.match -gt $end.match -and $s.phase -eq 'PlayerTurn' } 'new match and first turn'
    Confirm-Check ($reset.score -eq 0 -and $reset.moves -eq 15 -and @($reset.players | Where-Object alive).Count -eq 3) 'rematch resets shared and alive state'
    $fresh = $script:byId[$reset.activeId]
    $onTurn = Wait-State $fresh { param($s) (Get-Owner $s).rollStamp -eq $reset.stamp } 'rematch owner dice'
    Check-Ack $fresh "move $((Get-Owner $onTurn).die) @first" 'Move' $false
    'PASS: ejected caster and terminal actions rejected, nonhost rematch rejected, host rematch resets state, old match stamp rejected'
}


function Check-Session {
    Invoke-Peer $client1 "join $($Port + 1)"
    $trying = Invoke-Peer $client1 'state'
    Confirm-Check ($trying.menuVisible -and !$trying.lobbyVisible -and $trying.joined -eq 0) 'connection attempt did not show lobby'
    Invoke-Peer $client1 "join $Port"
    $failed = Wait-State $client1 { param($s) !$s.listening -and !$s.connected -and $s.menuVisible -and !$s.lobbyVisible -and $s.status -match '(?i)timed out|failed|could not|unable|unreachable|disconnect' } 'failed localhost connection' 65
    Confirm-Check ($failed.joined -eq 0 -and $failed.players.Count -eq 0) 'failed and duplicate connection did not join'
    'PASS: absent local host and duplicate pending attempt left menu usable with a failure status'

    Start-Room
    Join-Peer $client1 $Port
    $two = Wait-Roster $hostPeer 2
    Check-Ack $hostPeer 'start' 'Start' $false
    Invoke-Peer $hostPeer 'duplicate-lobby'
    Start-Sleep -Milliseconds 150
    $afterDuplicate = Invoke-Peer $hostPeer 'state'
    Confirm-Check ($afterDuplicate.connected -and $afterDuplicate.joined -eq $two.joined -and $afterDuplicate.stamp -eq $two.stamp) 'duplicate manager did not end or replace active lobby'

    Invoke-Peer $client1 'leave'
    $null = Wait-State $client1 { param($s) !$s.listening -and $s.menuVisible -and $s.players.Count -eq 0 } 'clean leave'
    $one = Wait-Roster $hostPeer 1
    Confirm-Check ($one.phase -eq 'Lobby' -and $one.winner -eq 'None') 'lobby departure did not apply match wins'
    Join-Peer $client1 $Port
    Join-Peer $client2 $Port
    $null = Wait-Roster $hostPeer 3
    'PASS: host minimum guard, duplicate lobby manager isolation and leave/rejoin without stale roster or lobby win'

    Invoke-Peer $hostPeer 'capacity 3'
    Invoke-Peer $latePeer "join $Port"
    $full = Wait-State $latePeer { param($s) !$s.listening -and $s.menuVisible -and $s.status -match '(?i)full|capacity' } 'capacity rejection' 20
    Confirm-Check ($full.joined -eq 0 -and $full.players.Count -eq 0) 'full lobby did not expose a connected roster'
    Invoke-Peer $hostPeer 'capacity 8'
    Invoke-Peer $hostPeer 'lengthen-turns'
    Check-Ack $hostPeer 'start' 'Start' $true
    $null = Wait-State $hostPeer { param($s) $s.phase -eq 'PlayerTurn' } 'active match'
    Invoke-Peer $latePeer "join $Port"
    $locked = Wait-State $latePeer { param($s) !$s.listening -and $s.menuVisible -and $s.status -match '(?i)progress|already|started|locked' } 'late join rejection' 20
    Confirm-Check ($locked.joined -eq 0 -and (Invoke-Peer $hostPeer 'state').players.Count -eq 3) 'late join did not change active roster'
    'PASS: server rejects over-capacity and active-match joins with private reasons'

    Stop-Process -InputObject $script:players[$hostPeer]
    foreach ($peer in @($client1,$client2)) {
        $null = Wait-State $peer { param($s) !$s.listening -and $s.menuVisible -and !$s.lobbyVisible -and $s.players.Count -eq 0 -and $s.status -match '(?i)host|disconnect|lost' } 'host loss and return to menu' 45
    }
    'PASS: abrupt host loss returned both clients to the menu with cleared rosters and a disconnect reason'
}

function Start-MatchWithPeers([string[]]$peers) {
    Start-Room
    foreach ($peer in $peers) {
        if ($peer -ne $hostPeer) { Join-Peer $peer $Port }
    }
    $null = Wait-Roster $hostPeer $peers.Count
    Invoke-Peer $hostPeer 'lengthen-turns'
    Check-Ack $hostPeer 'start' 'Start' $true
    $null = Wait-State $hostPeer { param($s) $s.phase -eq 'PlayerTurn' } 'active match'
    Check-PrivateViews $peers
}

function Stop-Room([string[]]$peers) {
    foreach ($peer in $peers) { if ($peer -ne $hostPeer) { Invoke-Peer $peer 'leave' } }
    Invoke-Peer $hostPeer 'leave'
    foreach ($peer in $peers) {
        $null = Wait-State $peer { param($s) !$s.listening -and $s.menuVisible -and $s.players.Count -eq 0 } 'session cleanup' 18
    }
}

function Check-Departure {
    $peers = @($primaryPeers + $latePeer)
    Start-MatchWithPeers $peers
    $before = Invoke-Peer $hostPeer 'state'
    $schedule = @(Invoke-Peer $hostPeer 'schedule')
    Confirm-Check ($schedule.Count -eq 4 -and $schedule[0] -eq $before.activeId) 'expected initial authority queue'
    $leaver = @($peers | Where-Object { $_ -ne $hostPeer -and $script:roles[$_] -eq 'Normal' -and $_ -ne $script:byId[$before.activeId] })[0]
    $leaverId = (Invoke-Peer $leaver 'state').localId
    Invoke-Peer $leaver 'leave'
    $remaining = Wait-Roster $hostPeer 3
    Confirm-Check ($remaining.activeId -eq $before.activeId -and $remaining.winner -eq 'None') 'inactive departure preserved current active turn'
    $expected = @($schedule | Where-Object { $_ -ne $leaverId })
    foreach ($expectedActor in $expected) {
        $at = Invoke-Peer $hostPeer 'state'
        Confirm-Check ($at.phase -eq 'PlayerTurn' -and $at.activeId -eq $expectedActor) 'inactive departure did not skip a surviving turn'
        Invoke-Peer $hostPeer 'advance-timer'
        $null = Wait-State $hostPeer { param($s) $s.stamp -ne $at.stamp -or $s.phase -ne 'PlayerTurn' } 'next scheduled turn'
    }
    $null = Wait-State $hostPeer { param($s) $s.phase -eq 'Discussion' } 'discussion after all surviving turns'
    'PASS: inactive normal departure preserved active turn and every scheduled surviving turn'

    Stop-Room $peers
    Start-MatchWithPeers $peers
    $schedule = @(Invoke-Peer $hostPeer 'schedule')
    $leaverId = @($schedule[0..2] | Where-Object { $script:byId[$_] -ne $hostPeer -and $script:roles[$script:byId[$_]] -eq 'Normal' })[0]
    $leaver = $script:byId[$leaverId]
    $position = [Array]::IndexOf($schedule,$leaverId)
    for ($turn = 0; $turn -lt $position; $turn++) {
        $at = Invoke-Peer $hostPeer 'state'
        Invoke-Peer $hostPeer 'advance-timer'
        $null = Wait-State $hostPeer { param($s) $s.stamp -ne $at.stamp } 'next scheduled turn'
    }
    $at = Invoke-Peer $hostPeer 'state'
    Confirm-Check ($at.activeId -eq $leaverId) 'target of departure is active'
    Invoke-Peer $leaver 'leave'
    $nextActor = $schedule[$position + 1]
    $next = Wait-State $hostPeer { param($s) $s.phase -eq 'PlayerTurn' -and $s.players.Count -eq 3 -and $s.activeId -eq $nextActor } 'next eligible player after active departure'
    Confirm-Check ($next.score -eq $at.score -and $next.moves -eq $at.moves -and $next.winner -eq 'None') 'active departure did not spend a move or end viable match'
    'PASS: active normal departure advanced once to next scheduled surviving player without spending a move'
}

function Wait-MatchPhase([string[]]$peers,[string]$phase,[int]$round) {
    foreach ($peer in $peers) {
        $null = Wait-State $peer { param($s) $s.phase -eq $phase -and $s.round -eq $round } "round $round $phase"
    }
}

function Complete-Ballot([string[]]$peers,[int]$round,[string]$ballot) {
    for ($turn = 0; $turn -lt $peers.Count; $turn++) {
        if ((Invoke-Peer $hostPeer 'state').phase -ne 'PlayerTurn') { break }
        Invoke-Peer $hostPeer 'finish-turn'
    }
    Invoke-Peer $hostPeer 'begin-vote'
    Wait-MatchPhase $peers 'Voting' $round
    foreach ($peer in $peers) {
        if ((Get-Owner (Invoke-Peer $peer 'state')).alive) {
            Check-Ack $peer $ballot 'Vote' $true
        }
    }
    $null = Wait-State $hostPeer { param($s) $s.phase -eq 'Elimination' } 'resolved ballot'
    Invoke-Peer $hostPeer 'advance-timer'
}

function Check-Budget {
    $peers = @($primaryPeers + $latePeer + 'ValidationClient4' + 'ValidationClient5')
    Start-Room
    foreach ($peer in $peers | Where-Object { $_ -ne $hostPeer }) { Join-Peer $peer $Port }
    $null = Wait-Roster $hostPeer 6
    Invoke-Peer $hostPeer 'lengthen-turns'
    for ($match = 0; $match -lt 3; $match++) {
        Invoke-Peer $hostPeer 'seed-host-imposter'
        if ($match -eq 0) { Check-Ack $hostPeer 'start' 'Start' $true }
        else { Check-Ack $hostPeer 'rematch' 'Rematch' $true }
        $reveals = @{}
        foreach ($peer in $peers) {
            $reveals[$peer] = Wait-State $peer { param($s) $s.roleVisible -and $s.roleText -in @('IMPOSTER','NORMAL PLAYER') } 'private role reveal'
        }
        foreach ($peer in $peers) {
            if ($reveals[$peer].roleText -eq 'IMPOSTER') {
                $teammate = @($peers | Where-Object { $_ -ne $peer -and $reveals[$_].roleText -eq 'IMPOSTER' })[0]
                $objective = [string]$reveals[$peer].roleObjective
                Confirm-Check ($teammate -and $objective.Contains((Get-Owner $reveals[$teammate]).name)) "$peer privately sees their teammate"
            } else {
                Confirm-Check ($reveals[$peer].roleObjective -notmatch 'Teammate') "$peer does not see imposter teammate names"
            }
        }
        Wait-MatchPhase $peers 'PlayerTurn' 1
        Check-PrivateViews $peers
        $remote = @($peers | Where-Object { $_ -ne $hostPeer -and $script:roles[$_] -eq 'Imposter' })[0]
        $imposters = @($hostPeer, $remote)
        foreach ($peer in $imposters) {
            $owner = Get-Owner (Invoke-Peer $peer 'state')
            Confirm-Check ($owner.role -eq 'Imposter' -and $owner.alive -and $owner.block -eq 1 -and $owner.curse -eq 2 -and $owner.shift -eq 1) "$peer fresh block, curse and team shift budgets in match $match"
        }
        if ($match -eq 2) { break }
        for ($round = 1; $round -le 4; $round++) {
            Wait-MatchPhase $peers 'PlayerTurn' $round
            $schedule = @(Invoke-Peer $hostPeer 'schedule')
            $target1 = $schedule[1]
            $target2 = $schedule[2]
            switch ($round) {
                1 {
                    Check-Ack $hostPeer "block $target1" 'ForcedBlock' $true
                    Check-Ack $remote "curse $target2" 'CursedDice' $true
                    Check-Ack $hostPeer "curse $target2" 'CursedDice' $false
                    Check-Ack $remote "block $target2" 'ForcedBlock' $false
                    $null = Wait-State $hostPeer { param($s) (Get-Owner $s).block -eq 0 -and (Get-Owner $s).curse -eq 2 } 'host spend and cap'
                    $null = Wait-State $remote { param($s) (Get-Owner $s).block -eq 1 -and (Get-Owner $s).curse -eq 1 } 'remote spend and cap'
                }
                2 {
                    Check-Ack $remote "block $target1" 'ForcedBlock' $true
                    Check-Ack $hostPeer "curse $target2" 'CursedDice' $true
                    $null = Wait-State $remote { param($s) (Get-Owner $s).block -eq 0 } 'remote block spend'
                }
                3 {
                    $shiftPeer, $otherPeer = $imposters[$match], $imposters[1 - $match]
                    Check-Ack $shiftPeer 'shift 1' 'LuckyShift' $true
                    Check-Ack $otherPeer 'shift 1' 'LuckyShift' $false
                    foreach ($peer in $imposters) {
                        $null = Wait-State $peer { param($s) (Get-Owner $s).shift -eq 0 } 'shared shift spent'
                    }
                    Check-Ack $remote "curse $target2" 'CursedDice' ($match -eq 0)
                    if ($match -eq 1) { Check-Ack $hostPeer "curse $target2" 'CursedDice' $true }
                }
                4 {
                    $secondCurse = if ($match -eq 0) { $hostPeer } else { $remote }
                    Check-Ack $secondCurse "curse $target2" 'CursedDice' $true
                    foreach ($peer in $imposters) {
                        $null = Wait-State $peer { param($s) (Get-Owner $s).curse -eq 0 } 'both curse charges consumed'
                    }
                }
            }
            Complete-Ballot $peers $round 'vote-skip'
        }
        $normalIds = @($peers | Where-Object { $script:roles[$_] -eq 'Normal' } | ForEach-Object { (Invoke-Peer $_ 'state').localId })
        for ($i = 0; $i -lt 2; $i++) {
            Wait-MatchPhase $peers 'PlayerTurn' (5 + $i)
            Complete-Ballot $peers (5 + $i) "vote $($normalIds[$i])"
        }
        Wait-MatchPhase $peers 'MatchEnded' 6
    }
    'PASS: host and remote imposters each spent block and both curses, enforced the round cap, shared one shift and restored all budgets on two rematches'
}

function Check-Spectator {
    $peers = @($primaryPeers + $latePeer + 'ValidationClient4' + 'ValidationClient5')
    Start-MatchWithPeers $peers
    $imposters = @($peers | Where-Object { $script:roles[$_] -eq 'Imposter' })
    Confirm-Check ($imposters.Count -eq 2) 'six players assigned two imposters'
    foreach ($peer in $peers) {
        $owner = Get-Owner (Invoke-Peer $peer 'state')
        $expectedTeammates = if ($owner.role -eq 'Imposter') { 1 } else { 0 }
        Confirm-Check ($owner.teammates -eq $expectedTeammates) "$peer teammate visibility"
    }
    $ejectedPeer = @($imposters | Where-Object { $_ -ne $hostPeer })[0]
    $ejectedId = (Invoke-Peer $ejectedPeer 'state').localId
    for ($turn = 0; $turn -lt 6; $turn++) {
        $at = Invoke-Peer $hostPeer 'state'
        if ($at.phase -eq 'Discussion') { break }
        Invoke-Peer $hostPeer 'advance-timer'
        $null = Wait-State $hostPeer { param($s) $s.stamp -ne $at.stamp -or $s.phase -ne 'PlayerTurn' } 'next scheduled turn'
    }
    Invoke-Peer $hostPeer 'advance-timer'
    foreach ($peer in $peers) {
        $null = Wait-State $peer { param($s) $s.phase -eq 'Voting' } 'voting'
        Check-Ack $peer "vote $ejectedId" 'Vote' $true
    }
    $null = Wait-State $hostPeer { param($s) $s.phase -eq 'Elimination' } 'nonterminal elimination'
    Invoke-Peer $hostPeer 'advance-timer'
    $null = Wait-State $ejectedPeer { param($s) $s.phase -eq 'PlayerTurn' -and $s.round -eq 2 -and !(Get-Owner $s).alive } 'ejected spectator in active round'
    $ownerBefore = Get-Owner (Invoke-Peer $ejectedPeer 'state')
    Confirm-Check ($ownerBefore.block -eq 1) 'dead caster retained a charge for the guard test'
    Check-Ack $ejectedPeer 'block 0' 'ForcedBlock' $false
    $ownerAfter = Get-Owner (Invoke-Peer $ejectedPeer 'state')
    Confirm-Check ($ownerAfter.block -eq $ownerBefore.block) 'dead caster did not spend a charge in active match'
    'PASS: six-player private teammate visibility and a charged ejected imposter rejected during active play'
}

function Check-UI {
    Start-Room
    Join-Peer $client1 $Port
    Join-Peer $client2 $Port
    foreach ($peer in $primaryPeers) {
        $null = Wait-State $peer { param($s) $s.lobbyCards.Count -eq 3 } 'three visible lobby cards'
    }
    $before = Invoke-Peer $client1 'state'
    Invoke-Peer $client1 'ui-ready'
    $after = Wait-State $client1 { param($s) (Get-Owner $s).ready } 'ready toggle'
    Confirm-Check (($before.lobbyCards -join ',') -eq ($after.lobbyCards -join ',')) 'ready update retained card instances'
    if ($Render) { Invoke-Peer $client1 'capture lobby' }
    Invoke-Peer $hostPeer 'lengthen-turns'
    Invoke-Peer $hostPeer 'seed-remote-imposter'
    Check-Ack $hostPeer 'start' 'Start' $true
    foreach ($peer in $primaryPeers) {
        $reveal = Wait-State $peer { param($s) $s.roleVisible -and $s.roleText -in @('IMPOSTER','NORMAL PLAYER') } 'owner role reveal'
        $expected = if ((Get-Owner $reveal).role -eq 'Imposter') { 'IMPOSTER' } else { 'NORMAL PLAYER' }
        Confirm-Check ($reveal.roleText -eq $expected -and !$reveal.lobbyVisible) "$peer role panel matches private snapshot"
    }
    if ($Render) { Invoke-Peer $client1 'capture role' }
    Wait-MatchPhase $primaryPeers 'PlayerTurn' 1
    Check-PrivateViews $primaryPeers
    $imposter = @($primaryPeers | Where-Object { $script:roles[$_] -eq 'Imposter' })[0]
    $first = Invoke-Peer $hostPeer 'state'
    $activePeer = $script:byId[$first.activeId]
    foreach ($peer in $primaryPeers) {
        $s = Invoke-Peer $peer 'state'
        Confirm-Check (!$s.roleVisible -and $s.diceVisible -eq ($peer -eq $activePeer) -and $s.abilityVisible -eq ($peer -eq $imposter)) "$peer only sees controls applicable to their role and turn"
    }
    $ability = Invoke-Peer $imposter 'state'
    Confirm-Check (($ability.shiftOptions -join ',') -eq '-5,-4,-3,-2,-1,+1,+2,+3,+4,+5') 'shift picker offers all ten deltas'
    Confirm-Check ($ability.targetOptions.Count -eq 2 -and @($ability.targetOptions | Where-Object { $_ -like '* (You)' }).Count -eq [int]($imposter -ne $activePeer)) 'upcoming-only target picker includes self when legal'
    if ($Render) {
        Invoke-Peer $imposter 'capture sabotage'
        Invoke-Peer $activePeer 'capture dice'
    }
    Check-Ack $imposter 'ui-shift -2' 'LuckyShift' $true
    $null = Wait-State $activePeer { param($s) $s.historyText -match 'Target shifted by -2' } 'rendered history before reactivation'
    Invoke-Peer $activePeer 'ui-reactivate-gameplay'
    $null = Wait-State $activePeer { param($s) $s.diceVisible -and $s.historyText -match 'Target shifted by -2' } 'reactivated private dice and public history'
    $die = (Get-Owner (Invoke-Peer $activePeer 'state')).die
    Check-Ack $activePeer 'ui-move -' 'Move' $true
    $second = Wait-State $hostPeer { param($s) $s.moves -eq 14 -and $s.activeId -ne $first.activeId } 'one move and next active player'
    Confirm-Check ($second.score -eq -$die) 'subtract control commits the negative face value'
    $nextPeer = $script:byId[$second.activeId]
    $null = Wait-State $nextPeer { param($s) $s.diceVisible -and $s.stamp -eq $second.stamp } 'next active UI'
    if ($activePeer -eq $hostPeer) {
        Check-Ack $nextPeer 'ui-move -' 'Move' $true
    } elseif ($nextPeer -eq $hostPeer) {
        Invoke-Peer $hostPeer 'finish-turn'
    }
    if ($activePeer -eq $hostPeer -or $nextPeer -eq $hostPeer) {
        $third = Wait-State $hostPeer { param($s) $s.phase -eq 'PlayerTurn' -and $s.stamp -ne $second.stamp } 'third active turn'
        $nextPeer = $script:byId[$third.activeId]
        $null = Wait-State $nextPeer { param($s) $s.diceVisible -and $s.stamp -eq $third.stamp } 'third active UI'
    }
    Confirm-Check ($nextPeer -ne $hostPeer) 'remote player can submit a voluntary skip'
    Check-Ack $nextPeer 'ui-skip' 'Skip' $true
    for ($round = 1; $round -le 3; $round++) {
        if ($round -eq 2) { Check-Ack $imposter 'ui-block' 'ForcedBlock' $true }
        if ($round -eq 3) { Check-Ack $imposter 'ui-curse' 'CursedDice' $true }
        Complete-Ballot $primaryPeers $round 'ui-vote-skip'
        Wait-MatchPhase $primaryPeers 'PlayerTurn' ($round + 1)
    }
    for ($replay = 0; $replay -le 2; $replay++) {
        $matchState = Invoke-Peer $hostPeer 'state'
        $imposterId = (Invoke-Peer $imposter 'state').localId
        Invoke-Peer $hostPeer 'begin-vote'
        Wait-MatchPhase $primaryPeers 'Voting' $matchState.round
        if ($Render -and $replay -eq 0) { Invoke-Peer $client1 'capture voting' }
        foreach ($peer in $primaryPeers) { Check-Ack $peer "ui-vote $imposterId" 'Vote' $true }
        Wait-MatchPhase $primaryPeers 'Elimination' $matchState.round
        if ($Render -and $replay -eq 0) { Invoke-Peer $client1 'capture elimination' }
        Invoke-Peer $hostPeer 'advance-timer'
        Wait-MatchPhase $primaryPeers 'MatchEnded' $matchState.round
        if ($Render -and $replay -eq 0) { Invoke-Peer $client1 'capture results' }
        if ($replay -eq 2) { break }
        Check-Ack $hostPeer 'ui-rematch' 'Rematch' $true
        Wait-MatchPhase $primaryPeers 'PlayerTurn' 1
        Check-PrivateViews $primaryPeers
        $imposter = @($primaryPeers | Where-Object { $script:roles[$_] -eq 'Imposter' })[0]
    }
    Invoke-Peer $client1 'ui-return'
    Stop-Room $primaryPeers
    foreach ($peer in $primaryPeers) {
        $s = Invoke-Peer $peer 'state'
        Confirm-Check ($s.menuVisible -and !$s.diceVisible -and !$s.abilityVisible -and !$s.roleVisible -and !$s.historyText) "$peer cleared match UI on exit"
    }
    'PASS: stable lobby cards, private role reveal and dice, panel hydration, ability selectors, gameplay and vote buttons, two UI rematches, session cleanup'
}

function Check-Presentation([string[]]$peers) {
    if (!$Render) { throw 'Presentation requires -Render.' }
    Start-Room
    foreach ($peer in $peers | Where-Object { $_ -ne $hostPeer }) {
        Join-Peer $peer $Port
    }
    for ($i = 0; $i -lt $peers.Count; $i++) {
        Invoke-Peer $peers[$i] "profile ExtremelyLongLoungePlayerNameNumber$i"
    }
    $null = Wait-State $client1 { param($s) $s.lobbyCards.Count -eq 8 } 'eight visible lobby cards'
    Invoke-Peer $client1 'capture lobby-eight'
    Invoke-Peer $hostPeer 'lengthen-turns'
    Check-Ack $hostPeer 'start' 'Start' $true
    Wait-MatchPhase $peers 'PlayerTurn' 1
    Check-PrivateViews $peers
    Invoke-Peer $hostPeer 'fill-history'
    $null = Wait-State $client1 { param($s) ($s.historyText -split "`n").Count -ge 30 } 'long public history'
    Invoke-Peer $client1 'capture gameplay-eight'
    for ($i = 0; $i -lt $peers.Count; $i++) {
        $current = Invoke-Peer $hostPeer 'state'
        if ($current.phase -ne 'PlayerTurn') { break }
        Invoke-Peer $hostPeer 'advance-timer'
        $null = Wait-State $hostPeer { param($s) $s.stamp -ne $current.stamp -or $s.phase -eq 'Discussion' } 'next turn or discussion'
    }
    Wait-MatchPhase $peers 'Discussion' 1
    Invoke-Peer $client1 'capture discussion-eight'
    Invoke-Peer $hostPeer 'advance-timer'
    Wait-MatchPhase $peers 'Voting' 1
    Invoke-Peer $client1 'capture voting-eight'
    $target = (Invoke-Peer $hostPeer 'state').localId
    foreach ($peer in $peers) { Check-Ack $peer "ui-vote $target" 'Vote' $true }
    Wait-MatchPhase $peers 'Elimination' 1
    Invoke-Peer $client1 'capture elimination-eight'
    'PASS: modal focus and raycast, settings preview, error banner, eight players with long names, long history, discussion and full ballot'
}

function Check-Relay {
    Invoke-Peer $hostPeer 'relay-host'
    $hostState = Wait-State $hostPeer { param($s) $s.connected -and $s.lobbyVisible -and $s.roomCode } 'online lobby creation'
    foreach ($peer in $primaryPeers | Where-Object { $_ -ne $hostPeer }) {
        Invoke-Peer $peer "relay-join $($hostState.roomCode)"
        $null = Wait-State $peer { param($s) $s.connected -and $s.lobbyVisible } 'online lobby join'
    }
    $null = Wait-Roster $hostPeer 3
    Invoke-Peer $hostPeer 'lengthen-turns'
    Check-Ack $hostPeer 'start' 'Start' $true
    Wait-MatchPhase $primaryPeers 'PlayerTurn' 1
    Check-PrivateViews $primaryPeers
    $turn = Invoke-Peer $hostPeer 'state'
    $activePeer = $script:byId[$turn.activeId]
    Check-Ack $activePeer 'ui-move -' 'Move' $true
    foreach ($peer in $primaryPeers) {
        $null = Wait-State $peer { param($s) $s.moves -eq 14 -and $s.score -lt 0 } 'replicated online move'
    }
    $imposter = @($primaryPeers | Where-Object { $script:roles[$_] -eq 'Imposter' })[0]
    $imposterId = (Invoke-Peer $imposter 'state').localId
    Invoke-Peer $hostPeer 'begin-vote'
    Wait-MatchPhase $primaryPeers 'Voting' 1
    foreach ($peer in $primaryPeers) { Check-Ack $peer "ui-vote $imposterId" 'Vote' $true }
    Wait-MatchPhase $primaryPeers 'Elimination' 1
    Invoke-Peer $hostPeer 'advance-timer'
    Wait-MatchPhase $primaryPeers 'MatchEnded' 1
    Check-Ack $hostPeer 'ui-rematch' 'Rematch' $true
    Wait-MatchPhase $primaryPeers 'PlayerTurn' 1
    Stop-Room $primaryPeers
    'PASS: real Unity Authentication, Lobby and Relay with three players, private views, UI move and ballots, rematch, and leave'
}

try {
    $participants = switch ($Scenario) {
        'Presentation' { $primaryPeers; $latePeer; 'ValidationClient4'; 'ValidationClient5'; 'ValidationClient6'; 'ValidationClient7' }
        { $_ -in 'Spectator','Budget' } { $primaryPeers; $latePeer; 'ValidationClient4'; 'ValidationClient5' }
        { $_ -in 'Session','Departure' } { $primaryPeers; $latePeer }
        default { $primaryPeers }
    }
    if ($Scenario -eq 'Presentation') {
        if (!$Render) { throw 'Presentation requires -Render.' }
        Start-Peer $client1
        Invoke-Peer $client1 'ui-menu-qa'
    }
    foreach ($peer in $participants) {
        if (!$script:players.ContainsKey($peer)) { Start-Peer $peer }
    }
    if ($Scenario -ne 'Review') {
        switch ($Scenario) {
            'Authority' { Check-Authority }
            'Session' { Check-Session }
            'Departure' { Check-Departure }
            'Spectator' { Check-Spectator }
            'Budget' { Check-Budget }
            'UI' { Check-UI }
            'Presentation' { Check-Presentation $participants }
            'Relay' { Check-Relay }
        }
        "PASS: $Scenario completed"
        return
    }
    for ($room = 0; $room -lt 2; $room++) {
        Start-Room
        Join-Peer $client1 $Port
        Join-Peer $client2 $Port
        $null = Wait-Roster $hostPeer 3
        Invoke-Peer $hostPeer 'lengthen-turns'
        Check-Ack $hostPeer 'start' 'Start' $true
        $first = Wait-State $hostPeer { param($s) $s.phase -eq 'PlayerTurn' } 'first turn'
        Check-PrivateViews $primaryPeers
        if ($room -eq 0) {
            $previousSession = $first.stamp.Split('/')[0]
            foreach ($peer in $primaryPeers) { Invoke-Peer $peer 'cache old-room' }
        } else {
            Confirm-Check ($first.stamp.Split('/')[0] -ne $previousSession) 'rehost uses a new session identifier'
            $activePeer = $script:byId[$first.activeId]
            $die = (Get-Owner (Invoke-Peer $activePeer 'state')).die
            Check-Ack $activePeer "move $die @old-room" 'Move' $false
            Confirm-Check ((Invoke-Peer $hostPeer 'state').moves -eq $first.moves) 'old session stamp did not consume the roll'
        }
        $imposter = @($primaryPeers | Where-Object { $script:roles[$_] -eq 'Imposter' })[0]
        Check-Ack $imposter 'shift 1' 'LuckyShift' $true
        foreach ($peer in $primaryPeers) {
            $s = Wait-State $peer { param($s) $s.publicActors.Count -gt 0 -and $s.historyText -match 'Target shifted' } 'anonymous shift history'
            Confirm-Check ($s.publicActors[-1] -eq 'TargetShift|18446744073709551615|') 'anonymous public payload'
            Confirm-Check ($s.historyText -notmatch "$imposter shifted") 'anonymous rendered history'
        }
        'PASS: public shift payload and UI are anonymous on all peers'
        $impId = (Invoke-Peer $imposter 'state').localId
        $otherId = @((Invoke-Peer $hostPeer 'state').players | Where-Object id -ne $impId)[0].id
        Invoke-Peer $hostPeer 'begin-vote'
        foreach ($peer in $primaryPeers) { $null=Wait-State $peer { param($s) $s.phase -eq 'Voting' } 'initial voting' }
        for ($ballot=0; $ballot -lt 3; $ballot++) {
            Check-Ack $hostPeer "vote $impId" 'Vote' $true
            Check-Ack $client1 "vote $otherId" 'Vote' $true
            Invoke-Peer $hostPeer 'advance-timer'
            if ($ballot -lt 2) {
                $expected=$ballot+1
                foreach ($peer in $primaryPeers) { $null=Wait-State $peer { param($s) $s.phase -eq 'Voting' -and $s.runoff -eq $expected -and !(Get-Owner $s).voted } 'open replicated runoff' }
            }
        }
        foreach ($peer in $primaryPeers) {
            $s=Wait-State $peer { param($s) $s.phase -eq 'Elimination' -and $s.winner -ne 'None' -and $s.revealVisible -and $s.revealText -match 'THEY WERE' } 'winning ejection reveal'
            Confirm-Check ($s.outcome -eq 'RandomEjection') 'third identical tie resolves random ejection'
        }
        Check-Ack $imposter 'shift 1' 'LuckyShift' $false
        Invoke-Peer $hostPeer 'advance-timer'
        foreach ($peer in $primaryPeers) { $null=Wait-State $peer { param($s) $s.phase -eq 'MatchEnded' } 'results after reveal' }
        'PASS: two runoffs accept ballots; third tied result shows winning reveal before results'
        foreach ($peer in @($client2,$client1,$hostPeer)) { Invoke-Peer $peer 'leave' }
        foreach ($peer in $primaryPeers) { $null=Wait-State $peer { param($s) !$s.listening -and $s.players.Count -eq 0 } 'shutdown' }
    }
    'PASS: review regressions and repeated rehost completed'
}
finally {
    foreach ($peer in $script:players.Keys) {
        $process=$script:players[$peer]
        if (!$process.HasExited) {
            try { Invoke-Peer $peer 'quit'; if (!$process.WaitForExit(2000)) { Stop-Process -InputObject $process } }
            catch { if (!$process.HasExited) { Stop-Process -InputObject $process } }
        }
        $process.Dispose()
    }
}
