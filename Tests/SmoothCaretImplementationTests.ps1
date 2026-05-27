$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$projectCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'TypeSunny.csproj')
$mainCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'UI\MainWindow.xaml.cs')
$copybookCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'UI\Modes\CopybookMode.cs')
$tracingCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'UI\Modes\TracingMode.cs')

function Assert-Match {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if ($Text -notmatch $Pattern) {
        throw $Name
    }
}

function Assert-NotMatch {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if ($Text -match $Pattern) {
        throw $Name
    }
}

Assert-Match 'Project should compile UI\SmoothCaret.cs.' $projectCode '<Compile Include="UI\\SmoothCaret\.cs"'
Assert-Match 'Project should compile UI\SmoothMotionTiming.cs.' $projectCode '<Compile Include="UI\\SmoothMotionTiming\.cs"'
Assert-Match 'Project should compile UI\SmoothBackground.cs.' $projectCode '<Compile Include="UI\\SmoothBackground\.cs"'
Assert-Match 'Project should compile Utils\SmoothScrollHelper.cs.' $projectCode '<Compile Include="Utils\\SmoothScrollHelper\.cs"'

Assert-Match 'MainWindow should use SmoothScrollHelper.' $mainCode 'SmoothScrollHelper\.AnimateScrollTo'
Assert-Match 'SmoothScrollTo should return whether scrolling happened.' $mainCode 'internal\s+bool\s+SmoothScrollTo\s*\('
Assert-Match 'SmoothScrollTo should accept started callback.' $mainCode 'Action\s+started\s*=\s*null'
Assert-Match 'SmoothScrollTo should accept completed callback.' $mainCode 'Action\s+completed\s*=\s*null'

Assert-Match 'CopybookMode should hold SmoothCaret instead of Border.' $copybookCode 'private\s+SmoothCaret\s+_cursor'
Assert-Match 'CopybookMode should support animated positioning.' $copybookCode 'UpdatePosition\s*\(\s*bool\s+animated\s*=\s*false\s*\)'
Assert-Match 'CopybookMode animated path should call AnimatePosition.' $copybookCode 'if\s*\(animated\)[\s\S]*?_cursor\.AnimatePosition'
Assert-Match 'CopybookMode non-animated path should call SetPosition.' $copybookCode 'else[\s\S]*?_cursor\.SetPosition'
Assert-Match 'CopybookMode should track position during smooth scroll without clearing caret animation.' $copybookCode '_isScrollAnimating[\s\S]*?_cursor\.TrackPosition'
Assert-Match 'CopybookMode should record dynamic motion timing after typed input.' $copybookCode '_cursor\?\.RecordInput\(\)'
Assert-Match 'CopybookMode should queue typed background updates for visual flush.' $copybookCode 'QueueDisplayBlockStateBackground'
Assert-Match 'CopybookMode should flush typed backgrounds together with animated caret advance.' $copybookCode 'ScheduleAdvanceVisuals[\s\S]*?FlushPendingBackgroundChanges\(\)[\s\S]*?UpdatePosition\s*\(\s*true\s*\)'
Assert-Match 'CopybookMode should replace older queued background for the same index.' $copybookCode 'QueueDisplayBlockStateBackground[\s\S]*?RemovePendingBackgroundChange\(globalIndex\)[\s\S]*?_pendingBackgroundChanges\.Add'
Assert-Match 'CopybookMode backspace should cancel queued typed background before clearing it.' $copybookCode 'TextInfo\.wordStates\[_currentIndex\]\s*=\s*WordStates\.NO_TYPE;[\s\S]*?RemovePendingBackgroundChange\(_currentIndex\)[\s\S]*?SetDisplayBlockStateBackgroundByGlobalIndex\(_currentIndex,\s*null\)'
Assert-Match 'CopybookMode should sync caret while smooth scrolling.' $copybookCode 'CompositionTarget\.Rendering\s*\+='
Assert-Match 'CopybookMode ScrollToCurrentChar should pass scroll sync callbacks.' $copybookCode 'SmoothScrollTo\s*\([^;]*StartScrollSync[^;]*StopScrollSync'
Assert-Match 'CopybookMode should keep scroll-tracking state while doing final scroll position sync.' $copybookCode 'StopScrollSync[\s\S]*?UpdatePosition\s*\(\s*false\s*\)[\s\S]*?_isScrollAnimating\s*=\s*false'
Assert-NotMatch 'CopybookMode should not set Canvas.Left directly on _cursor.' $copybookCode 'Canvas\.SetLeft\(_cursor'
Assert-NotMatch 'CopybookMode should not set Canvas.Top directly on _cursor.' $copybookCode 'Canvas\.SetTop\(_cursor'

Assert-Match 'TracingMode should hold SmoothCaret instead of Border.' $tracingCode 'private\s+SmoothCaret\s+_cursor'
Assert-Match 'TracingMode should support animated positioning.' $tracingCode 'UpdatePosition\s*\(\s*bool\s+animated\s*=\s*false\s*\)'
Assert-Match 'TracingMode animated path should call AnimatePosition.' $tracingCode 'if\s*\(animated\)[\s\S]*?_cursor\.AnimatePosition'
Assert-Match 'TracingMode non-animated path should call SetPosition.' $tracingCode 'else[\s\S]*?_cursor\.SetPosition'
Assert-Match 'TracingMode should track position during smooth scroll without clearing caret animation.' $tracingCode '_isScrollAnimating[\s\S]*?_cursor\.TrackPosition'
Assert-Match 'TracingMode should record dynamic motion timing after typed input.' $tracingCode '_cursor\?\.RecordInput\(\)'
Assert-Match 'TracingMode should queue typed background updates for visual flush.' $tracingCode 'QueueDisplayBlockStateBackground'
Assert-Match 'TracingMode should flush typed backgrounds together with animated caret advance.' $tracingCode 'FlushPendingBackgroundChanges\(\)[\s\S]*?UpdatePosition\s*\(\s*true\s*\)'
Assert-Match 'TracingMode should replace older queued background for the same index.' $tracingCode 'QueueDisplayBlockStateBackground[\s\S]*?RemovePendingBackgroundChange\(globalIndex\)[\s\S]*?_pendingBackgroundChanges\.Add'
Assert-Match 'TracingMode backspace should cancel queued typed background before clearing it.' $tracingCode 'TextInfo\.wordStates\[_currentIndex\]\s*=\s*WordStates\.NO_TYPE;[\s\S]*?RemovePendingBackgroundChange\(_currentIndex\)[\s\S]*?SetDisplayBlockStateBackgroundByGlobalIndex\(_currentIndex,\s*null\)'
Assert-Match 'TracingMode should sync caret while smooth scrolling.' $tracingCode 'CompositionTarget\.Rendering\s*\+='
Assert-Match 'TracingMode should update caret on manual display scroll.' $tracingCode 'ScDisplay\.ScrollChanged\s*\+=\s*OnDisplayScrollChanged'
Assert-Match 'TracingMode should unsubscribe manual display scroll updates.' $tracingCode 'ScDisplay\.ScrollChanged\s*-=\s*OnDisplayScrollChanged'
Assert-Match 'TracingMode manual scroll should reposition without animation.' $tracingCode 'OnDisplayScrollChanged[\s\S]*?UpdatePosition\s*\(\s*false\s*\)'
Assert-Match 'TracingMode ScrollToCurrentChar should pass scroll sync callbacks.' $tracingCode 'SmoothScrollTo\s*\([^;]*StartScrollSync[^;]*StopScrollSync'
Assert-Match 'TracingMode should keep scroll-tracking state while doing final scroll position sync.' $tracingCode 'StopScrollSync[\s\S]*?UpdatePosition\s*\(\s*false\s*\)[\s\S]*?_isScrollAnimating\s*=\s*false'
Assert-NotMatch 'TracingMode should not set Canvas.Left directly on _cursor.' $tracingCode 'Canvas\.SetLeft\(_cursor'
Assert-NotMatch 'TracingMode should not set Canvas.Top directly on _cursor.' $tracingCode 'Canvas\.SetTop\(_cursor'

Write-Host 'Smooth caret implementation structure tests passed.'
