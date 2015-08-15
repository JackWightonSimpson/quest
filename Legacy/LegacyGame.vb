Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Linq
Imports System.Net

Public Class LegacyGame

    Implements IASL
    Implements IASLTimer

    Public Enum State
        Ready       ' game is not doing any processing, and is ready for a command
        Working     ' game is processing a command
        Waiting     ' while processing a command, game has encountered e.g. an "enter" script, and is awaiting further input
        Finished    ' game is over
    End Enum

    Private Class DefineBlock
        Public StartLine As Integer
        Public EndLine As Integer
    End Class

    Friend Class Context
        Public CallingObjectId As Integer
        Public NumParameters As Integer
        Public Parameters() As String
        Public FunctionReturnValue As String
        Public AllowRealNamesInCommand As Boolean
        Public DontProcessCommand As Boolean
        Public CancelExec As Boolean
        Public StackCounter As Integer
    End Class

    Private Function CopyContext(ctx As Context) As Context
        Dim result As Context = New Context
        result.CallingObjectID = ctx.CallingObjectID
        result.NumParameters = ctx.NumParameters
        result.Parameters = ctx.Parameters
        result.FunctionReturnValue = ctx.FunctionReturnValue
        result.AllowRealNamesInCommand = ctx.AllowRealNamesInCommand
        result.DontProcessCommand = ctx.DontProcessCommand
        result.CancelExec = ctx.CancelExec
        result.StackCounter = ctx.StackCounter
        Return result
    End Function

    Private _openErrorReport As String
    Private _casKeywords(255) As String 'Tokenised CAS keywords
    Private _lines() As String 'Stores the lines of the ASL script/definitions
    Private _defineBlocks() As DefineBlock 'Stores the start and end lines of each 'define' section
    Private _numberSections As Integer 'Number of define sections
    Private _gameName As String 'The name of the game
    Friend _nullContext As New Context

    Friend Enum LogType
        Misc
        FatalError
        WarningError
        Init
        LibraryWarningError
        Warning
        UserError
        InternalError
    End Enum

    Private _defineBlockParams As Dictionary(Of String, Dictionary(Of String, String))

    Friend Enum Direction
        None = -1
        Out = 0
        North = 1
        South = 2
        East = 3
        West = 4
        NorthWest = 5
        NorthEast = 6
        SouthWest = 7
        SouthEast = 8
        Up = 9
        Down = 10
    End Enum

    Private Class ItemType
        Public Name As String
        Public Got As Boolean
    End Class

    Private Class Collectable
        Public Name As String
        Public Type As String
        Public Value As Double
        Public Display As String
        Public DisplayWhenZero As Boolean
    End Class

    Friend Class PropertyType
        Public PropertyName As String
        Public PropertyValue As String
    End Class

    Friend Class ActionType
        Public ActionName As String
        Public Script As String
    End Class

    Friend Class UseDataType
        Public UseObject As String
        Public UseType As UseType
        Public UseScript As String
    End Class

    Friend Class GiveDataType
        Public GiveObject As String
        Public GiveType As GiveType
        Public GiveScript As String
    End Class

    Private Class PropertiesActions
        Public Properties As String
        Public NumberActions As Integer
        Public Actions() As ActionType
        Public NumberTypesIncluded As Integer
        Public TypesIncluded() As String
    End Class

    Private Class VariableType
        Public VariableName As String
        Public VariableContents() As String
        Public VariableUBound As Integer
        Public DisplayString As String
        Public OnChangeScript As String
        Public NoZeroDisplay As Boolean
    End Class

    Private Class SynonymType
        Public OriginalWord As String
        Public ConvertTo As String
    End Class

    Private Class TimerType
        Public TimerName As String
        Public TimerInterval As Integer
        Public TimerActive As Boolean
        Public TimerAction As String
        Public TimerTicks As Integer
        Public BypassThisTurn As Boolean
    End Class

    Friend Class UserDefinedCommandType
        Public CommandText As String
        Public CommandScript As String
    End Class

    Friend Class TextAction
        Public Data As String
        Public Type As TextActionType
    End Class

    Friend Enum TextActionType
        Text
        Script
        [Nothing]
        [Default]
    End Enum

    Friend Class ScriptText
        Public Text As String
        Public Script As String
    End Class

    Friend Class PlaceType
        Public PlaceName As String
        Public Prefix As String
        Public PlaceAlias As String
        Public Script As String
    End Class

    Friend Class RoomType
        Public RoomName As String
        Public RoomAlias As String
        Public Commands() As UserDefinedCommandType
        Public NumberCommands As Integer
        Public Description As New TextAction
        Public Out As New ScriptText
        Public East As New TextAction
        Public West As New TextAction
        Public North As New TextAction
        Public South As New TextAction
        Public NorthEast As New TextAction
        Public NorthWest As New TextAction
        Public SouthEast As New TextAction
        Public SouthWest As New TextAction
        Public Up As New TextAction
        Public Down As New TextAction
        Public InDescription As String
        Public Look As String
        Public Places() As PlaceType
        Public NumberPlaces As Integer
        Public Prefix As String
        Public Script As String
        Public Use() As ScriptText
        Public NumberUse As Integer
        Public ObjId As Integer
        Public BeforeTurnScript As String
        Public AfterTurnScript As String
        Public Exits As RoomExits
    End Class

    Friend Class ObjectType
        Public ObjectName As String
        Public ObjectAlias As String
        Public Detail As String
        Public ContainerRoom As String
        Public Exists As Boolean
        Public IsGlobal As Boolean
        Public Prefix As String
        Public Suffix As String
        Public Gender As String
        Public Article As String
        Public DefinitionSectionStart As Integer
        Public DefinitionSectionEnd As Integer
        Public Visible As Boolean
        Public GainScript As String
        Public LoseScript As String
        Public NumberProperties As Integer
        Public Properties() As PropertyType
        Public Speak As New TextAction
        Public Take As New TextAction
        Public IsRoom As Boolean
        Public IsExit As Boolean
        Public CorresRoom As String
        Public CorresRoomId As Integer
        Public Loaded As Boolean
        Public NumberActions As Integer
        Public Actions() As ActionType
        Public NumberUseData As Integer
        Public UseData() As UseDataType
        Public UseAnything As String
        Public UseOnAnything As String
        Public Use As String
        Public NumberGiveData As Integer
        Public GiveData() As GiveDataType
        Public GiveAnything As String
        Public GiveToAnything As String
        Public DisplayType As String
        Public NumberTypesIncluded As Integer
        Public TypesIncluded() As String
        Public NumberAltNames As Integer
        Public AltNames() As String
        Public AddScript As New TextAction
        Public RemoveScript As New TextAction
        Public OpenScript As New TextAction
        Public CloseScript As New TextAction
    End Class

    Private Class ChangeType
        Public AppliesTo As String
        Public Change As String
    End Class

    Private Class GameChangeDataType
        Public NumberChanges As Integer
        Public ChangeData() As ChangeType
    End Class

    Private Class ResourceType
        Public ResourceName As String
        Public ResourceStart As Integer
        Public ResourceLength As Integer
        Public Extracted As Boolean
    End Class

    Private Class ExpressionResult
        Public Result As String
        Public Success As ExpressionSuccess
        Public Message As String
    End Class

    Private _changeLogRooms As ChangeLog
    Private _changeLogObjects As ChangeLog
    Private _defaultProperties As PropertiesActions
    Private _defaultRoomProperties As PropertiesActions

    Friend _rooms() As RoomType
    Friend _numberRooms As Integer
    Private _numericVariable() As VariableType
    Private _numberNumericVariables As Integer
    Private _stringVariable() As VariableType
    Private _numberStringVariables As Integer
    Private _synonyms() As SynonymType
    Private _numberSynonyms As Integer
    Private _items() As ItemType
    Private _chars() As ObjectType
    Friend _objs() As ObjectType
    Private _numberChars As Integer
    Friend _numberObjs As Integer
    Private _numberItems As Integer
    Friend _currentRoom As String
    Private _collectables() As Collectable
    Private _numCollectables As Integer
    Private _gamePath As String
    Private _gameFileName As String
    Private _saveGameFile As String
    Private _defaultFontName As String
    Private _defaultFontSize As Double
    Private _autoIntro As Boolean
    Private _commandOverrideModeOn As Boolean
    Private _commandOverrideVariable As String
    Private _afterTurnScript As String
    Private _beforeTurnScript As String
    Private _outPutOn As Boolean
    Private _gameAslVersion As Integer
    Private _choiceNumber As Integer
    Private _gameLoadMethod As String    ' TODO: Make enum
    Private _timers() As TimerType
    Private _numberTimers As Integer
    Private _numDisplayStrings As Integer
    Private _numDisplayNumerics As Integer
    Private _gameFullyLoaded As Boolean
    Private _gameChangeData As New GameChangeDataType
    Private _lastIt As Integer
    Private _lastItMode As ItType
    Private _thisTurnIt As Integer
    Private _thisTurnItMode As ItType
    Private _badCmdBefore As String
    Private _badCmdAfter As String
    Private _numResources As Integer
    Private _resources() As ResourceType
    Private _resourceFile As String
    Private _resourceOffset As Integer
    Private _startCatPos As Integer
    Private _useAbbreviations As Boolean
    Private _loadedFromQsg As Boolean  ' TODO: Same as _gameLoadMethod
    Private _beforeSaveScript As String
    Private _onLoadScript As String
    Private _numSkipCheckFiles As Integer
    Private _skipCheckFile() As String
    Private _compassExits As New List(Of ListData)
    Private _gotoExits As New List(Of ListData)
    Private _textFormatter As New TextFormatter
    Private _log As New List(Of String)
    Private _fileData As String
    Private _commandLock As Object = New Object
    Private _stateLock As Object = New Object
    Private _state As State = State.Ready
    Private _waitLock As Object = New Object
    Private _readyForCommand As Boolean = True
    Private _gameLoading As Boolean
    Private _random As New Random()
    Private _tempFolder As String
    Private _playerErrorMessageString(38) As String

    Friend Enum PlayerError
        BadCommand
        BadGo
        BadGive
        BadCharacter
        NoItem
        ItemUnwanted
        BadLook
        BadThing
        DefaultLook
        DefaultSpeak
        BadItem
        DefaultTake
        BadUse
        DefaultUse
        DefaultOut
        BadPlace
        BadExamine
        DefaultExamine
        BadTake
        CantDrop
        DefaultDrop
        BadDrop
        BadPronoun
        AlreadyOpen
        AlreadyClosed
        CantOpen
        CantClose
        DefaultOpen
        DefaultClose
        BadPut
        CantPut
        DefaultPut
        CantRemove
        AlreadyPut
        DefaultRemove
        Locked
        DefaultWait
        AlreadyTaken
    End Enum

    Private Enum ItType
        Inanimate
        Male
        Female
    End Enum

    Private Enum SetResult
        [Error]
        Found
        Unfound
    End Enum

    Private Enum Thing
        Character
        [Object]
        Room
    End Enum

    Private Enum ConvertType
        Strings
        Functions
        Numeric
        Collectables
    End Enum

    Friend Enum UseType
        UseOnSomething
        UseSomethingOn
    End Enum

    Friend Enum GiveType
        GiveToSomething
        GiveSomethingTo
    End Enum

    Private Enum VarType
        [String]
        Numeric
    End Enum

    Private Enum StopType
        Win
        Lose
        Null
    End Enum

    Private Enum ExpressionSuccess
        OK
        Fail
    End Enum

    Private m_listVerbs As New Dictionary(Of ListType, List(Of String))
    Private m_filename As String
    Private m_originalFilename As String
    Private m_data As InitGameData
    Private m_player As IPlayer
    Private m_gameFinished As Boolean
    Private m_gameIsRestoring As Boolean
    Private m_useStaticFrameForPictures As Boolean

    Public Sub New(filename As String, originalFilename As String)
        _tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath, "Quest", Guid.NewGuid().ToString())
        LoadCASKeywords()
        _gameLoadMethod = "normal"
        m_filename = filename
        m_originalFilename = originalFilename

        ' Very early versions of Quest didn't perform very good syntax checking of ASL files, so this is
        ' for compatibility with games which have non-fatal errors in them.
        _numSkipCheckFiles = 3
        ReDim _skipCheckFile(3)
        _skipCheckFile(1) = "bargain.cas"
        _skipCheckFile(2) = "easymoney.asl"
        _skipCheckFile(3) = "musicvf1.cas"
    End Sub

    Public Class InitGameData
        Public Data As Byte()
        Public SourceFile As String
    End Class

    Public Sub New(data As InitGameData)
        Me.New(Nothing, Nothing)
        m_data = data
    End Sub

    Private Function StripCodes(InputString As String) As String
        Dim FCodeDat As String
        Dim FCodePos, FCodeLen As Integer

        Do
            FCodePos = InStr(InputString, "|")
            If FCodePos <> 0 Then
                FCodeDat = Mid(InputString, FCodePos + 1, 3)

                If Left(FCodeDat, 1) = "b" Then
                    FCodeLen = 1
                ElseIf Left(FCodeDat, 2) = "xb" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 1) = "u" Then
                    FCodeLen = 1
                ElseIf Left(FCodeDat, 2) = "xu" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 1) = "i" Then
                    FCodeLen = 1
                ElseIf Left(FCodeDat, 2) = "xi" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 2) = "cr" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 2) = "cb" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 2) = "cl" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 2) = "cy" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 2) = "cg" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 1) = "n" Then
                    FCodeLen = 1
                ElseIf Left(FCodeDat, 2) = "xn" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 1) = "s" Then
                    FCodeLen = 3
                ElseIf Left(FCodeDat, 2) = "jc" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 2) = "jl" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 2) = "jr" Then
                    FCodeLen = 2
                ElseIf Left(FCodeDat, 1) = "w" Then
                    FCodeLen = 1
                ElseIf Left(FCodeDat, 1) = "c" Then
                    FCodeLen = 1
                End If

                If FCodeLen = 0 Then
                    ' unknown code
                    FCodeLen = 1
                End If

                InputString = Left(InputString, FCodePos - 1) & Mid(InputString, FCodePos + FCodeLen + 1)
            End If

        Loop Until FCodePos = 0

        Return InputString

    End Function

    Private Function CheckSections() As Boolean
        Dim Defines, i, Braces As Integer
        Dim CheckLine As String = ""
        Dim BracePos As Integer
        Dim CurPos As Integer
        Dim ThisSection As String = ""
        Dim HasErrors As Boolean
        Dim bSkipBlock As Boolean
        _openErrorReport = ""
        HasErrors = False
        Defines = 0
        Braces = 0

        For i = 1 To UBound(_lines)
            If Not BeginsWith(_lines(i), "#!qdk-note: ") Then
                If BeginsWith(_lines(i), "define ") Then
                    ThisSection = _lines(i)
                    Braces = 0
                    Defines = Defines + 1
                    bSkipBlock = BeginsWith(_lines(i), "define text") Or BeginsWith(_lines(i), "define synonyms")
                ElseIf Trim(_lines(i)) = "end define" Then
                    Defines = Defines - 1

                    If Defines < 0 Then
                        LogASLError("Extra 'end define' after block '" & ThisSection & "'", LogType.FatalError)
                        _openErrorReport = _openErrorReport & "Extra 'end define' after block '" & ThisSection & "'" & vbCrLf
                        HasErrors = True
                        Defines = 0
                    End If

                    If Braces > 0 Then
                        LogASLError("Missing } in block '" & ThisSection & "'", LogType.FatalError)
                        _openErrorReport = _openErrorReport & "Missing } in block '" & ThisSection & "'" & vbCrLf
                        HasErrors = True
                    ElseIf Braces < 0 Then
                        LogASLError("Too many } in block '" & ThisSection & "'", LogType.FatalError)
                        _openErrorReport = _openErrorReport & "Too many } in block '" & ThisSection & "'" & vbCrLf
                        HasErrors = True
                    End If
                End If

                If Left(_lines(i), 1) <> "'" And Not bSkipBlock Then
                    CheckLine = ObliterateParameters(_lines(i))
                    If BeginsWith(CheckLine, "'<ERROR;") Then
                        ' ObliterateParameters denotes a mismatched $, ( etc.
                        ' by prefixing line with '<ERROR;*; where * is the mismatched
                        ' character
                        LogASLError("Expected closing " & Mid(CheckLine, 9, 1) & " character in '" & ReportErrorLine(_lines(i)) & "'", LogType.FatalError)
                        _openErrorReport = _openErrorReport & "Expected closing " & Mid(CheckLine, 9, 1) & " character in '" & ReportErrorLine(_lines(i)) & "'." & vbCrLf
                        Return False
                    End If
                End If

                If Left(Trim(CheckLine), 1) <> "'" Then
                    ' Now check {
                    CurPos = 1
                    Do
                        BracePos = InStr(CurPos, CheckLine, "{")
                        If BracePos <> 0 Then
                            CurPos = BracePos + 1
                            Braces = Braces + 1
                        End If
                    Loop Until BracePos = 0 Or CurPos > Len(CheckLine)

                    ' Now check }
                    CurPos = 1
                    Do
                        BracePos = InStr(CurPos, CheckLine, "}")
                        If BracePos <> 0 Then
                            CurPos = BracePos + 1
                            Braces = Braces - 1
                        End If
                    Loop Until BracePos = 0 Or CurPos > Len(CheckLine)
                End If
            End If
        Next i

        If Defines > 0 Then
            LogASLError("Missing 'end define'", LogType.FatalError)
            _openErrorReport = _openErrorReport & "Missing 'end define'." & vbCrLf
            HasErrors = True
        End If

        Return Not HasErrors
    End Function

    Private Function ConvertFriendlyIfs() As Boolean
        ' Converts
        '   if (%something% < 3) then ...
        ' to
        '   if is <%something%;lt;3> then ...
        ' and also repeat until ...

        ' Returns False if successful

        Dim ConvPos, i, SymbPos As Integer
        Dim Symbol As String
        Dim EndParamPos, j As Integer
        Dim ParamData As String
        Dim StartParamPos As Integer
        Dim FirstData, SecondData As String
        Dim ObscureLine, NewParam, VarObscureLine As String
        Dim BracketCount As Integer

        For i = 1 To UBound(_lines)
            ObscureLine = ObliterateParameters(_lines(i))
            ConvPos = InStr(ObscureLine, "if (")
            If ConvPos = 0 Then
                ConvPos = InStr(ObscureLine, "until (")
            End If
            If ConvPos = 0 Then
                ConvPos = InStr(ObscureLine, "while (")
            End If
            If ConvPos = 0 Then
                ConvPos = InStr(ObscureLine, "not (")
            End If
            If ConvPos = 0 Then
                ConvPos = InStr(ObscureLine, "and (")
            End If
            If ConvPos = 0 Then
                ConvPos = InStr(ObscureLine, "or (")
            End If


            If ConvPos <> 0 Then
                VarObscureLine = ObliterateVariableNames(_lines(i))
                If BeginsWith(VarObscureLine, "'<ERROR;") Then
                    ' ObliterateVariableNames denotes a mismatched #, % or $
                    ' by prefixing line with '<ERROR;*; where * is the mismatched
                    ' character
                    LogASLError("Expected closing " & Mid(VarObscureLine, 9, 1) & " character in '" & ReportErrorLine(_lines(i)) & "'", LogType.FatalError)
                    Return True
                End If
                StartParamPos = InStr(ConvPos, _lines(i), "(")

                EndParamPos = 0
                BracketCount = 1
                For j = StartParamPos + 1 To Len(_lines(i))
                    If Mid(_lines(i), j, 1) = "(" Then
                        BracketCount = BracketCount + 1
                    ElseIf Mid(_lines(i), j, 1) = ")" Then
                        BracketCount = BracketCount - 1
                    End If
                    If BracketCount = 0 Then
                        EndParamPos = j
                        Exit For
                    End If
                Next j

                'EndParamPos = InStr(ConvPos, VarObscureLine, ")")
                If EndParamPos = 0 Then
                    LogASLError("Expected ) in '" & ReportErrorLine(_lines(i)) & "'", LogType.FatalError)
                    Return True
                End If

                ParamData = Mid(_lines(i), StartParamPos + 1, (EndParamPos - StartParamPos) - 1)

                SymbPos = InStr(ParamData, "!=")
                If SymbPos = 0 Then
                    SymbPos = InStr(ParamData, "<>")
                    If SymbPos = 0 Then
                        SymbPos = InStr(ParamData, "<=")
                        If SymbPos = 0 Then
                            SymbPos = InStr(ParamData, ">=")
                            If SymbPos = 0 Then
                                SymbPos = InStr(ParamData, "<")
                                If SymbPos = 0 Then
                                    SymbPos = InStr(ParamData, ">")
                                    If SymbPos = 0 Then
                                        SymbPos = InStr(ParamData, "=")
                                        If SymbPos = 0 Then
                                            LogASLError("Unrecognised 'if' condition in '" & ReportErrorLine(_lines(i)) & "'", LogType.FatalError)
                                            Return True
                                        Else
                                            Symbol = "="
                                        End If
                                    Else
                                        Symbol = ">"
                                    End If
                                Else
                                    Symbol = "<"
                                End If
                            Else
                                Symbol = ">="
                            End If
                        Else
                            Symbol = "<="
                        End If
                    Else
                        Symbol = "<>"
                    End If
                Else
                    Symbol = "<>"
                End If


                FirstData = Trim(Left(ParamData, SymbPos - 1))
                SecondData = Trim(Mid(ParamData, SymbPos + Len(Symbol)))

                If Symbol = "=" Then
                    NewParam = "is <" & FirstData & ";" & SecondData & ">"
                Else
                    NewParam = "is <" & FirstData & ";"
                    If Symbol = "<" Then
                        NewParam = NewParam & "lt"
                    ElseIf Symbol = ">" Then
                        NewParam = NewParam & "gt"
                    ElseIf Symbol = ">=" Then
                        NewParam = NewParam & "gt="
                    ElseIf Symbol = "<=" Then
                        NewParam = NewParam & "lt="
                    ElseIf Symbol = "<>" Then
                        NewParam = NewParam & "!="
                    End If
                    NewParam = NewParam & ";" & SecondData & ">"
                End If

                _lines(i) = Left(_lines(i), StartParamPos - 1) & NewParam & Mid(_lines(i), EndParamPos + 1)

                ' Repeat processing this line, in case there are
                ' further changes to be made.
                i = i - 1
            End If
        Next i

        Return False
    End Function

    Private Sub ConvertMultiLineSections()

        Dim StartLine, BraceCount As Integer
        Dim ThisLine, LineToAdd As String
        Dim LastBrace As Integer
        Dim k, i, j, M As Integer
        Dim RestOfLine, ProcName As String
        Dim EndLineNum As Integer
        Dim AfterLastBrace, Z As String
        Dim StartOfOrig As String

        Dim TestLine As String
        Dim TestBraceCount As Integer
        Dim OBP, CBP As Integer
        Dim CurProc As Integer

        i = 1
        Do
            Z = _lines(_defineBlocks(i).StartLine)
            If ((Not BeginsWith(Z, "define text ")) And (Not BeginsWith(Z, "define menu ")) And Z <> "define synonyms") Then
                For j = _defineBlocks(i).StartLine + 1 To _defineBlocks(i).EndLine - 1
                    If InStr(_lines(j), "{") > 0 Then

                        AfterLastBrace = ""
                        ThisLine = Trim(_lines(j))

                        ProcName = "<!intproc" & Trim(Str(CurProc)) & ">"

                        ' see if this brace's corresponding closing
                        ' brace is on same line:

                        TestLine = Mid(_lines(j), InStr(_lines(j), "{") + 1)
                        TestBraceCount = 1
                        Do
                            OBP = InStr(TestLine, "{")
                            CBP = InStr(TestLine, "}")
                            If OBP = 0 Then OBP = Len(TestLine) + 1
                            If CBP = 0 Then CBP = Len(TestLine) + 1
                            If OBP < CBP Then
                                TestBraceCount = TestBraceCount + 1
                                TestLine = Mid(TestLine, OBP + 1)
                            ElseIf CBP < OBP Then
                                TestBraceCount = TestBraceCount - 1
                                TestLine = Mid(TestLine, CBP + 1)
                            End If
                        Loop Until OBP = CBP Or TestBraceCount = 0

                        If TestBraceCount <> 0 Then
                            AddLine("define procedure " & ProcName)
                            StartLine = UBound(_lines)
                            RestOfLine = Trim(Right(ThisLine, Len(ThisLine) - InStr(ThisLine, "{")))
                            BraceCount = 1
                            If RestOfLine <> "" Then AddLine(RestOfLine)

                            For M = 1 To Len(RestOfLine)
                                If Mid(RestOfLine, M, 1) = "{" Then
                                    BraceCount = BraceCount + 1
                                ElseIf Mid(RestOfLine, M, 1) = "}" Then
                                    BraceCount = BraceCount - 1
                                End If
                            Next M

                            If BraceCount <> 0 Then
                                k = j + 1
                                Do
                                    For M = 1 To Len(_lines(k))
                                        If Mid(_lines(k), M, 1) = "{" Then
                                            BraceCount = BraceCount + 1
                                        ElseIf Mid(_lines(k), M, 1) = "}" Then
                                            BraceCount = BraceCount - 1
                                        End If

                                        If BraceCount = 0 Then
                                            LastBrace = M
                                            Exit For
                                        End If
                                    Next M

                                    If BraceCount <> 0 Then
                                        'put Lines(k) into another variable, as
                                        'AddLine ReDims Lines, which it can't do if
                                        'passed Lines(x) as a parameter.
                                        LineToAdd = _lines(k)
                                        AddLine(LineToAdd)
                                    Else
                                        AddLine(Left(_lines(k), LastBrace - 1))
                                        AfterLastBrace = Trim(Mid(_lines(k), LastBrace + 1))
                                    End If

                                    'Clear original line
                                    _lines(k) = ""
                                    k = k + 1
                                Loop While BraceCount <> 0
                            End If

                            AddLine("end define")
                            EndLineNum = UBound(_lines)

                            _numberSections = _numberSections + 1
                            ReDim Preserve _defineBlocks(_numberSections)
                            _defineBlocks(_numberSections) = New DefineBlock
                            _defineBlocks(_numberSections).StartLine = StartLine
                            _defineBlocks(_numberSections).EndLine = EndLineNum

                            'Change original line where the { section
                            'started to call the new procedure.
                            StartOfOrig = Trim(Left(ThisLine, InStr(ThisLine, "{") - 1))
                            _lines(j) = StartOfOrig & " do " & ProcName & " " & AfterLastBrace
                            CurProc = CurProc + 1

                            ' Process this line again in case there was stuff after the last brace that included
                            ' more braces. e.g. } else {
                            j = j - 1
                        End If
                    End If
                Next j
            End If
            i = i + 1
        Loop Until i > _numberSections

        ' Join next-line "else"s to corresponding "if"s

        For i = 1 To _numberSections
            Z = _lines(_defineBlocks(i).StartLine)
            If ((Not BeginsWith(Z, "define text ")) And (Not BeginsWith(Z, "define menu ")) And Z <> "define synonyms") Then
                For j = _defineBlocks(i).StartLine + 1 To _defineBlocks(i).EndLine - 1
                    If BeginsWith(_lines(j), "else ") Then

                        'Go upwards to find "if" statement that this
                        'belongs to

                        For k = j To _defineBlocks(i).StartLine + 1 Step -1
                            If BeginsWith(_lines(k), "if ") Or InStr(ObliterateParameters(_lines(k)), " if ") <> 0 Then
                                _lines(k) = _lines(k) & " " & Trim(_lines(j))
                                _lines(j) = ""
                                k = _defineBlocks(i).StartLine
                            End If
                        Next k
                    End If
                Next j
            End If
        Next i


    End Sub

    Private Function ErrorCheck() As Boolean
        ' Parses ASL script for errors. Returns TRUE if OK;
        ' False if a critical error is encountered.
        Dim iCurBegin, iCurEnd As Integer
        Dim bHasErrors As Boolean
        Dim iCurPos As Integer
        Dim iNumParamStart, iNumParamEnd As Integer
        Dim bFinLoop, bInText As Boolean
        Dim i As Integer

        bHasErrors = False
        bInText = False

        ' Checks for incorrect number of < and > :
        For i = 1 To UBound(_lines)
            iNumParamStart = 0
            iNumParamEnd = 0

            If BeginsWith(_lines(i), "define text ") Then
                bInText = True
            End If
            If bInText And Trim(LCase(_lines(i))) = "end define" Then
                bInText = False
            End If

            If Not bInText Then
                'Find number of <'s:
                iCurPos = 1
                bFinLoop = False
                Do
                    If InStr(iCurPos, _lines(i), "<") <> 0 Then
                        iNumParamStart = iNumParamStart + 1
                        iCurPos = InStr(iCurPos, _lines(i), "<") + 1
                    Else
                        bFinLoop = True
                    End If
                Loop Until bFinLoop

                'Find number of >'s:
                iCurPos = 1
                bFinLoop = False
                Do
                    If InStr(iCurPos, _lines(i), ">") <> 0 Then
                        iNumParamEnd = iNumParamEnd + 1
                        iCurPos = InStr(iCurPos, _lines(i), ">") + 1
                    Else
                        bFinLoop = True
                    End If
                Loop Until bFinLoop

                If iNumParamStart > iNumParamEnd Then
                    LogASLError("Expected > in " & ReportErrorLine(_lines(i)), LogType.FatalError)
                    bHasErrors = True
                ElseIf iNumParamStart < iNumParamEnd Then
                    LogASLError("Too many > in " & ReportErrorLine(_lines(i)), LogType.FatalError)
                    bHasErrors = True
                End If
            End If
        Next i

        'Exit if errors found
        If bHasErrors = True Then
            Return True
        End If

        ' Checks that define sections have parameters:
        For i = 1 To _numberSections
            iCurBegin = _defineBlocks(i).StartLine
            iCurEnd = _defineBlocks(i).EndLine

            If BeginsWith(_lines(iCurBegin), "define game") Then
                If InStr(_lines(iCurBegin), "<") = 0 Then
                    LogASLError("'define game' has no parameter - game has no name", LogType.FatalError)
                    Return True
                End If
            Else
                If Not BeginsWith(_lines(iCurBegin), "define synonyms") And Not BeginsWith(_lines(iCurBegin), "define options") Then
                    If InStr(_lines(iCurBegin), "<") = 0 Then
                        LogASLError(_lines(iCurBegin) & " has no parameter", LogType.FatalError)
                        bHasErrors = True
                    End If
                End If
            End If
        Next i

        Return bHasErrors
    End Function

    Private Function GetAfterParameter(InputLine As String) As String
        ' Returns everything after the end of the first parameter
        ' in a string, i.e. for "use <thing> do <myproc>" it
        ' returns "do <myproc>"
        Dim EOP As Integer
        EOP = InStr(InputLine, ">")

        If EOP = 0 Or EOP + 1 > Len(InputLine) Then
            Return ""
        Else
            Return Trim(Mid(InputLine, EOP + 1))
        End If

    End Function

    Private Function ObliterateParameters(InputLine As String) As String

        Dim bInParameter As Boolean
        Dim ExitCharacter As String = ""
        Dim CurChar As String
        Dim OutputLine As String = ""
        Dim ObscuringFunctionName As Boolean
        Dim i As Integer

        bInParameter = False

        For i = 1 To Len(InputLine)
            CurChar = Mid(InputLine, i, 1)

            If bInParameter Then
                If ExitCharacter = ")" Then
                    If InStr("$#%", CurChar) > 0 Then
                        ' We might be converting a line like:
                        '   if ( $rand(1;10)$ < 3 ) then {
                        ' and we don't want it to end up like this:
                        '   if (~~~~~~~~~~~)$ <~~~~~~~~~~~
                        ' which will cause all sorts of confustion. So,
                        ' we get rid of everything between the $ characters
                        ' in this case, and set a flag so we know what we're
                        ' doing.

                        ObscuringFunctionName = True
                        ExitCharacter = CurChar

                        ' Move along please

                        OutputLine = OutputLine & "~"
                        i = i + 1
                        CurChar = Mid(InputLine, i, 1)
                    End If
                End If
            End If

            If Not bInParameter Then
                OutputLine = OutputLine & CurChar
                If CurChar = "<" Then
                    bInParameter = True
                    ExitCharacter = ">"
                End If
                If CurChar = "(" Then
                    bInParameter = True
                    ExitCharacter = ")"
                End If
            Else
                If CurChar = ExitCharacter Then
                    If Not ObscuringFunctionName Then
                        bInParameter = False
                        OutputLine = OutputLine & CurChar
                    Else
                        ' We've finished obscuring the function name,
                        ' now let's find the next ) as we were before
                        ' we found this dastardly function
                        ObscuringFunctionName = False
                        ExitCharacter = ")"
                        OutputLine = OutputLine & "~"
                    End If
                Else
                    OutputLine = OutputLine & "~"
                End If
            End If
        Next i

        If bInParameter Then
            Return "'<ERROR;" & ExitCharacter & ";" & OutputLine
        Else
            Return OutputLine
        End If

    End Function

    Private Function ObliterateVariableNames(InputLine As String) As String
        Dim bInParameter As Boolean
        Dim ExitCharacter As String = ""
        Dim OutputLine As String = ""
        Dim CurChar As String
        Dim i As Integer

        bInParameter = False

        For i = 1 To Len(InputLine)
            CurChar = Mid(InputLine, i, 1)
            If Not bInParameter Then
                OutputLine = OutputLine & CurChar
                If CurChar = "$" Then
                    bInParameter = True
                    ExitCharacter = "$"
                End If
                If CurChar = "#" Then
                    bInParameter = True
                    ExitCharacter = "#"
                End If
                If CurChar = "%" Then
                    bInParameter = True
                    ExitCharacter = "%"
                End If
                ' The ~ was for collectables, and this syntax only
                ' exists in Quest 2.x. The ~ was only finally
                ' allowed to be present on its own in ASL 320.
                If CurChar = "~" And _gameAslVersion < 320 Then
                    bInParameter = True
                    ExitCharacter = "~"
                End If
            Else
                If CurChar = ExitCharacter Then
                    bInParameter = False
                    OutputLine = OutputLine & CurChar
                Else
                    OutputLine = OutputLine & "X"
                End If
            End If
        Next i

        If bInParameter Then
            OutputLine = "'<ERROR;" & ExitCharacter & ";" & OutputLine
        End If

        Return OutputLine

    End Function

    Private Sub RemoveComments()
        Dim i, AposPos As Integer
        Dim InTextBlock As Boolean
        Dim InSynonymsBlock As Boolean
        Dim OblitLine As String

        ' If in a synonyms block, we want to remove lines which are comments, but
        ' we don't want to remove synonyms that contain apostrophes, so we only
        ' get rid of lines with an "'" at the beginning or with " '" in them

        For i = 1 To UBound(_lines)

            If BeginsWith(_lines(i), "'!qdk-note:") Then
                _lines(i) = "#!qdk-note:" & GetEverythingAfter(_lines(i), "'!qdk-note:")
            Else
                If BeginsWith(_lines(i), "define text ") Then
                    InTextBlock = True
                ElseIf Trim(_lines(i)) = "define synonyms" Then
                    InSynonymsBlock = True
                ElseIf BeginsWith(_lines(i), "define type ") Then
                    InSynonymsBlock = True
                ElseIf Trim(_lines(i)) = "end define" Then
                    InTextBlock = False
                    InSynonymsBlock = False
                End If

                If Not InTextBlock And Not InSynonymsBlock Then
                    If InStr(_lines(i), "'") > 0 Then
                        OblitLine = ObliterateParameters(_lines(i))
                        If Not BeginsWith(OblitLine, "'<ERROR;") Then
                            AposPos = InStr(OblitLine, "'")

                            If AposPos <> 0 Then
                                _lines(i) = Trim(Left(_lines(i), AposPos - 1))
                            End If
                        End If
                    End If
                ElseIf InSynonymsBlock Then
                    If Left(Trim(_lines(i)), 1) = "'" Then
                        _lines(i) = ""
                    Else
                        ' we look for " '", not "'" in synonyms lines
                        AposPos = InStr(ObliterateParameters(_lines(i)), " '")
                        If AposPos <> 0 Then
                            _lines(i) = Trim(Left(_lines(i), AposPos - 1))
                        End If
                    End If
                End If
            End If

        Next i
    End Sub

    Private Function ReportErrorLine(InputLine As String) As String
        ' We don't want to see the "!intproc" in logged error reports lines.
        ' This function replaces these "do" lines with a nicer-looking "..." for error reporting.

        Dim ReplaceFrom As Integer
        Dim OutputLine As String

        ReplaceFrom = InStr(InputLine, "do <!intproc")
        If ReplaceFrom <> 0 Then
            OutputLine = Left(InputLine, ReplaceFrom - 1) & "..."
        Else
            OutputLine = InputLine
        End If

        Return OutputLine
    End Function

    Private Function YesNo(yn As Boolean) As String
        If yn = True Then Return "Yes" Else Return "No"
    End Function

    Private Function IsYes(yn As String) As Boolean
        If LCase(yn) = "yes" Then IsYes = True Else IsYes = False
    End Function

    Friend Function BeginsWith(s As String, T As String) As Boolean
        ' Compares the beginning of the line with a given
        ' string. Case insensitive.

        ' Example: beginswith("Hello there","HeLlO")=TRUE

        Dim TT As Integer

        TT = Len(T)

        Return Left(LTrim(LCase(s)), TT) = LCase(T)
    End Function

    Private Function ConvertCASKeyword(CASchar As String) As String

        Dim c As Byte = System.Text.Encoding.GetEncoding(1252).GetBytes(CASchar)(0)
        Dim CK As String = _casKeywords(c)

        If CK = "!cr" Then
            CK = vbCrLf
        End If

        Return CK

    End Function

    Private Sub ConvertMultiLines()
        'Goes through each section capable of containing
        'script commands and puts any multiple-line script commands
        'into separate procedures. Also joins multiple-line "if"
        'statements.

        'This calls RemoveComments after joining lines, so that lines
        'with "'" as part of a multi-line parameter are not destroyed,
        'before looking for braces.

        Dim i As Integer

        For i = UBound(_lines) To 1 Step -1
            If Right(_lines(i), 2) = "__" Then
                _lines(i) = Left(_lines(i), Len(_lines(i)) - 2) & LTrim(_lines(i + 1))
                _lines(i + 1) = ""
                'Recalculate this line again
                i = i + 1
            ElseIf Right(_lines(i), 1) = "_" Then
                _lines(i) = Left(_lines(i), Len(_lines(i)) - 1) & LTrim(_lines(i + 1))
                _lines(i + 1) = ""
                'Recalculate this line again
                i = i + 1
            End If
        Next i

        RemoveComments()

    End Sub

    Private Function GetDefineBlock(blockname As String) As DefineBlock

        ' Returns the start and end points of a named block.
        ' Returns 0 if block not found.

        Dim i As Integer
        Dim l, BlockType As String

        Dim result = New DefineBlock
        result.StartLine = 0
        result.EndLine = 0

        For i = 1 To _numberSections
            ' Get the first line of the define section:
            l = _lines(_defineBlocks(i).StartLine)

            ' Now, starting from the first word after 'define',
            ' retrieve the next word and compare it to blockname:

            ' Add a space for define blocks with no parameter
            If InStr(8, l, " ") = 0 Then l = l & " "

            BlockType = Mid(l, 8, InStr(8, l, " ") - 8)

            If BlockType = blockname Then
                ' Return the start and end points
                result.StartLine = _defineBlocks(i).StartLine
                result.EndLine = _defineBlocks(i).EndLine
                i = _numberSections
            End If
        Next i

        Return result
    End Function

    Private Function DefineBlockParam(blockname As String, Param As String) As DefineBlock

        ' Returns the start and end points of a named block

        Dim i As Integer
        Dim sBlockType As String
        Dim SP As Integer
        Dim oCache As Dictionary(Of String, String)
        Dim sBlockName As String
        Dim asBlock() As String

        Dim result = New DefineBlock

        Param = "k" & Param ' protect against numeric block names

        If Not _defineBlockParams.ContainsKey(blockname) Then
            ' Lazily create cache of define block params

            oCache = New Dictionary(Of String, String)
            _defineBlockParams.Add(blockname, oCache)

            For i = 1 To _numberSections
                ' get the word after "define", e.g. "procedure"
                sBlockType = GetEverythingAfter(_lines(_defineBlocks(i).StartLine), "define ")
                SP = InStr(sBlockType, " ")
                If SP <> 0 Then
                    sBlockType = Trim(Left(sBlockType, SP - 1))
                End If

                If sBlockType = blockname Then
                    sBlockName = RetrieveParameter(_lines(_defineBlocks(i).StartLine), _nullContext, False)

                    sBlockName = "k" & sBlockName

                    If Not oCache.ContainsKey(sBlockName) Then
                        oCache.Add(sBlockName, _defineBlocks(i).StartLine & "," & _defineBlocks(i).EndLine)
                    Else
                        ' silently ignore duplicates
                    End If
                End If
            Next i
        Else
            oCache = _defineBlockParams.Item(blockname)
        End If

        If oCache.ContainsKey(Param) Then
            asBlock = Split(oCache.Item(Param), ",")
            result.StartLine = CInt(asBlock(0))
            result.EndLine = CInt(asBlock(1))
        End If

        Return result

    End Function

    Friend Function GetEverythingAfter(TheString As String, thetext As String) As String

        Dim l As Integer

        If Len(thetext) > Len(TheString) Then
            Return ""
        End If
        l = Len(thetext)
        Return Right(TheString, Len(TheString) - l)
    End Function

    Private Function Keyword2CAS(KWord As String) As String

        Dim k As String
        Dim i As Integer

        If KWord = "" Then
            Return ""
        End If
        k = ""

        For i = 0 To 255
            If LCase(KWord) = LCase(_casKeywords(i)) Then
                k = Chr(i)
                i = 255
            End If
        Next i

        If k = "" Then
            Return Keyword2CAS("!unknown") & KWord & Keyword2CAS("!unknown")
        Else
            Return k
        End If

    End Function

    Private Sub LoadCASKeywords()

        'Loads data required for conversion of CAS files

        Dim SemiColonPos, FH, KNum As Integer
        Dim KWord As String

        FH = FreeFile()

        Dim QuestDatLines As String() = GetResourceLines(My.Resources.QuestDAT)

        For Each line As String In QuestDatLines
            If Left(line, 1) <> "#" Then
                'Lines isn't a comment - so parse it.
                SemiColonPos = InStr(line, ";")
                KWord = Trim(Left(line, SemiColonPos - 1))
                KNum = CInt(Right(line, Len(line) - SemiColonPos))
                _casKeywords(KNum) = KWord
            End If
        Next

        Exit Sub

    End Sub

    Private Function GetResourceLines(res As Byte()) As String()
        Dim enc As New System.Text.UTF8Encoding()
        Dim resFile As String = enc.GetString(res)
        Return Split(resFile, Chr(13) + Chr(10))
    End Function

    Private Function ParseFile(ByRef Filename As String) As Boolean
        'Returns FALSE if failed.

        Dim FileNum As Integer
        Dim bHasErrors As Boolean
        Dim bResult As Boolean
        Dim LibCode(0) As String
        Dim LibLines As Integer
        Dim IgnoreMode, SkipCheck As Boolean
        Dim l, c, i, D, j As Integer
        Dim LibFileHandle As Integer
        Dim LibResourceLines As String()
        Dim LibFile As String
        Dim LibLine As String
        Dim InDefGameBlock, GameLine As Integer
        Dim InDefSynBlock, SynLine As Integer
        Dim LibFoundThisSweep As Boolean
        Dim LibFileName As String
        Dim LibraryList(0) As String
        Dim NumLibraries As Integer
        Dim LibraryAlreadyIncluded As Boolean
        Dim InDefTypeBlock As Integer
        Dim TypeBlockName As String
        Dim TypeLine As Integer
        Dim DefineCount, CurLine As Integer

        _defineBlockParams = New Dictionary(Of String, Dictionary(Of String, String))

        bResult = True

        FileNum = FreeFile()
        ' Parses file and returns the positions of each main
        ' 'define' block. Supports nested defines.

        If LCase(Right(Filename, 4)) = ".zip" Then
            m_originalFilename = Filename
            Filename = GetUnzippedFile(Filename)
            _gamePath = System.IO.Path.GetDirectoryName(Filename)
        End If

        If LCase(Right(Filename, 4)) = ".asl" Or LCase(Right(Filename, 4)) = ".txt" Then
            'Read file into Lines array
            Dim fileData As String

            If Config.ReadGameFileFromAzureBlob Then
                Using client As New WebClient
                    fileData = client.DownloadString(Filename)

                    Dim fileBytes As Byte() = client.DownloadData(Filename)
                    m_gameId = TextAdventures.Utility.Utility.CalculateMD5Hash(fileBytes)
                End Using
            Else
                fileData = System.IO.File.ReadAllText(Filename)
            End If

            Dim aslLines As String() = fileData.Split(Chr(13))
            ReDim _lines(aslLines.Length)
            _lines(0) = ""

            For l = 1 To aslLines.Length
                _lines(l) = aslLines(l - 1)
                _lines(l) = RemoveTabs(_lines(l))
                _lines(l) = _lines(l).Trim(" "c, Chr(10), Chr(13))
            Next

            l = aslLines.Length

        ElseIf LCase(Right(Filename, 4)) = ".cas" Then
            LogASLError("Loading CAS")
            LoadCASFile(Filename)
            l = UBound(_lines)

        Else
            Throw New InvalidOperationException("Unrecognized file extension")
        End If

        Dim LibVer As Integer

        ' Add libraries to end of code:

        NumLibraries = 0

        Do
            LibFoundThisSweep = False
            For i = l To 1 Step -1
                ' We search for includes backwards as a game might include
                ' some-general.lib and then something-specific.lib which needs
                ' something-general; if we include something-specific first,
                ' then something-general afterwards, something-general's startscript
                ' gets executed before something-specific's, as we execute the
                ' lib startscripts backwards as well
                If BeginsWith(_lines(i), "!include ") Then
                    LibFileName = RetrieveParameter(_lines(i), _nullContext)
                    'Clear !include statement
                    _lines(i) = ""
                    LibraryAlreadyIncluded = False
                    LogASLError("Including library '" & LibFileName & "'...", LogType.Init)

                    For j = 1 To NumLibraries
                        If LCase(LibFileName) = LCase(LibraryList(j)) Then
                            LibraryAlreadyIncluded = True
                            j = NumLibraries
                        End If
                    Next j

                    If LibraryAlreadyIncluded Then
                        LogASLError("     - Library already included.", LogType.Init)
                    Else
                        NumLibraries = NumLibraries + 1
                        ReDim Preserve LibraryList(NumLibraries)
                        LibraryList(NumLibraries) = LibFileName

                        LibFoundThisSweep = True
                        LibResourceLines = Nothing

                        LibFile = _gamePath & LibFileName
                        LogASLError(" - Searching for " & LibFile & " (game path)", LogType.Init)
                        LibFileHandle = FreeFile()

                        If System.IO.File.Exists(LibFile) Then
                            FileOpen(LibFileHandle, LibFile, OpenMode.Input)
                        Else
                            ' File was not found; try standard Quest libraries (stored here as resources)

                            LogASLError("     - Library not found in game path.", LogType.Init)
                            LogASLError(" - Searching for " & LibFile & " (standard libraries)", LogType.Init)
                            LibResourceLines = GetLibraryLines(LibFileName)

                            If LibResourceLines Is Nothing Then
                                LogASLError("Library not found.", LogType.FatalError)
                                _openErrorReport = _openErrorReport & "Library '" & LibraryList(NumLibraries) & "' not found." & vbCrLf
                                Return False
                            End If
                        End If

                        LogASLError("     - Found library, opening...", LogType.Init)

                        LibLines = 0

                        If LibResourceLines Is Nothing Then
                            Do Until EOF(LibFileHandle)
                                LibLines = LibLines + 1
                                LibLine = LineInput(LibFileHandle)
                                LibLine = RemoveTabs(LibLine)
                                ReDim Preserve LibCode(LibLines)
                                LibCode(LibLines) = Trim(LibLine)
                            Loop
                            FileClose(LibFileHandle)
                        Else
                            For Each ResLibLine As String In LibResourceLines
                                LibLines = LibLines + 1
                                ReDim Preserve LibCode(LibLines)
                                LibLine = ResLibLine
                                LibLine = RemoveTabs(LibLine)
                                LibCode(LibLines) = Trim(LibLine)
                            Next
                        End If

                        LibVer = -1

                        If LibCode(1) = "!library" Then
                            For c = 1 To LibLines
                                If BeginsWith(LibCode(c), "!asl-version ") Then
                                    LibVer = CInt(RetrieveParameter(LibCode(c), _nullContext))
                                    c = LibLines
                                End If
                            Next c
                        Else
                            'Old library
                            LibVer = 100
                        End If

                        If LibVer = -1 Then
                            LogASLError(" - Library has no asl-version information.", LogType.LibraryWarningError)
                            LibVer = 200
                        End If

                        IgnoreMode = False
                        For c = 1 To LibLines
                            If BeginsWith(LibCode(c), "!include ") Then
                                ' Quest only honours !include in a library for asl-version
                                ' 311 or later, as it ignored them in versions < 3.11
                                If LibVer >= 311 Then
                                    AddLine(LibCode(c))
                                    l = l + 1
                                End If
                            ElseIf Left(LibCode(c), 1) <> "!" And Left(LibCode(c), 1) <> "'" And Not IgnoreMode Then
                                AddLine(LibCode(c))
                                l = l + 1
                            Else
                                If LibCode(c) = "!addto game" Then
                                    InDefGameBlock = 0
                                    For D = 1 To UBound(_lines)
                                        If BeginsWith(_lines(D), "define game ") Then
                                            InDefGameBlock = 1
                                        ElseIf BeginsWith(_lines(D), "define ") Then
                                            If InDefGameBlock <> 0 Then
                                                InDefGameBlock = InDefGameBlock + 1
                                            End If
                                        ElseIf _lines(D) = "end define" And InDefGameBlock = 1 Then
                                            GameLine = D
                                            D = UBound(_lines)
                                        ElseIf _lines(D) = "end define" Then
                                            If InDefGameBlock <> 0 Then
                                                InDefGameBlock = InDefGameBlock - 1
                                            End If
                                        End If
                                    Next D

                                    Do
                                        c = c + 1
                                        If Not BeginsWith(LibCode(c), "!end") Then
                                            ReDim Preserve _lines(UBound(_lines) + 1)
                                            For D = UBound(_lines) To GameLine + 1 Step -1
                                                _lines(D) = _lines(D - 1)
                                            Next D

                                            ' startscript lines in a library are prepended
                                            ' with "lib" internally so they are executed
                                            ' before any startscript specified by the
                                            ' calling ASL file, for asl-versions 311 and
                                            ' later.

                                            ' similarly, commands in a library. NB: without this, lib
                                            ' verbs have lower precedence than game verbs anyway. Also
                                            ' lib commands have lower precedence than game commands. We
                                            ' only need this code so that game verbs have a higher
                                            ' precedence than lib commands.

                                            ' we also need it so that lib verbs have a higher
                                            ' precedence than lib commands.

                                            If LibVer >= 311 And BeginsWith(LibCode(c), "startscript ") Then
                                                _lines(GameLine) = "lib " & LibCode(c)
                                            ElseIf LibVer >= 392 And (BeginsWith(LibCode(c), "command ") Or BeginsWith(LibCode(c), "verb ")) Then
                                                _lines(GameLine) = "lib " & LibCode(c)
                                            Else
                                                _lines(GameLine) = LibCode(c)
                                            End If

                                            l = l + 1
                                            GameLine = GameLine + 1
                                        End If
                                    Loop Until BeginsWith(LibCode(c), "!end")
                                ElseIf LibCode(c) = "!addto synonyms" Then
                                    InDefSynBlock = 0
                                    For D = 1 To UBound(_lines)
                                        If _lines(D) = "define synonyms" Then
                                            InDefSynBlock = 1
                                        ElseIf _lines(D) = "end define" And InDefSynBlock = 1 Then
                                            SynLine = D
                                            D = UBound(_lines)
                                        End If
                                    Next D

                                    If InDefSynBlock = 0 Then
                                        'No "define synonyms" block in game - so add it
                                        AddLine("define synonyms")
                                        AddLine("end define")
                                        SynLine = UBound(_lines)
                                    End If

                                    Do
                                        c = c + 1
                                        If Not BeginsWith(LibCode(c), "!end") Then
                                            ReDim Preserve _lines(UBound(_lines) + 1)
                                            For D = UBound(_lines) To SynLine + 1 Step -1
                                                _lines(D) = _lines(D - 1)
                                            Next D
                                            _lines(SynLine) = LibCode(c)
                                            l = l + 1
                                            SynLine = SynLine + 1
                                        End If
                                    Loop Until BeginsWith(LibCode(c), "!end")
                                ElseIf BeginsWith(LibCode(c), "!addto type ") Then
                                    InDefTypeBlock = 0
                                    TypeBlockName = LCase(RetrieveParameter(LibCode(c), _nullContext))
                                    For D = 1 To UBound(_lines)
                                        If LCase(_lines(D)) = "define type <" & TypeBlockName & ">" Then
                                            InDefTypeBlock = 1
                                        ElseIf _lines(D) = "end define" And InDefTypeBlock = 1 Then
                                            TypeLine = D
                                            D = UBound(_lines)
                                        End If
                                    Next D

                                    If InDefTypeBlock = 0 Then
                                        'No "define type (whatever)" block in game - so add it
                                        AddLine("define type <" & TypeBlockName & ">")
                                        AddLine("end define")
                                        TypeLine = UBound(_lines)
                                    End If

                                    Do
                                        c = c + 1
                                        If c > LibLines Then Exit Do
                                        If Not BeginsWith(LibCode(c), "!end") Then
                                            ReDim Preserve _lines(UBound(_lines) + 1)
                                            For D = UBound(_lines) To TypeLine + 1 Step -1
                                                _lines(D) = _lines(D - 1)
                                            Next D
                                            _lines(TypeLine) = LibCode(c)
                                            l = l + 1
                                            TypeLine = TypeLine + 1
                                        End If
                                    Loop Until BeginsWith(LibCode(c), "!end")


                                ElseIf LibCode(c) = "!library" Then
                                    'ignore
                                ElseIf BeginsWith(LibCode(c), "!asl-version ") Then
                                    'ignore
                                ElseIf BeginsWith(LibCode(c), "'") Then
                                    'ignore
                                ElseIf BeginsWith(LibCode(c), "!QDK") Then
                                    IgnoreMode = True
                                ElseIf BeginsWith(LibCode(c), "!end") Then
                                    IgnoreMode = False
                                End If
                            End If
                        Next c
                    End If
                End If
            Next i
        Loop Until LibFoundThisSweep = False

        SkipCheck = False

        Dim FilenameNoPath As String
        Dim LastSlashPos, SlashPos As Integer
        Dim CurPos As Integer
        CurPos = 1
        Do
            SlashPos = InStr(CurPos, Filename, "\")
            If SlashPos <> 0 Then LastSlashPos = SlashPos
            CurPos = SlashPos + 1
        Loop Until SlashPos = 0
        FilenameNoPath = LCase(Mid(Filename, LastSlashPos + 1))

        For i = 1 To _numSkipCheckFiles
            If FilenameNoPath = _skipCheckFile(i) Then
                SkipCheck = True
                i = _numSkipCheckFiles
            End If
        Next i

        If FilenameNoPath = "musicvf1.cas" Then
            m_useStaticFrameForPictures = True
        End If

        'RemoveComments called within ConvertMultiLines
        ConvertMultiLines()

        If Not SkipCheck Then
            If Not CheckSections() Then
                Return False
            End If
        End If

        _numberSections = 1

        For i = 1 To l
            ' find section beginning with 'define'
            If BeginsWith(_lines(i), "define") = True Then
                ' Now, go through until we reach an 'end define'. However, if we
                ' encounter another 'define' there is a nested define. So, if we
                ' encounter 'define' we increment the definecount. When we find an
                ' 'end define' we decrement it. When definecount is zero, we have
                ' found the end of the section.

                DefineCount = 1

                ' Don't count the current line - we know it begins with 'define'...
                CurLine = i + 1
                Do
                    If BeginsWith(_lines(CurLine), "define") = True Then
                        DefineCount = DefineCount + 1
                    ElseIf BeginsWith(_lines(CurLine), "end define") = True Then
                        DefineCount = DefineCount - 1
                    End If
                    CurLine = CurLine + 1
                Loop Until DefineCount = 0
                CurLine = CurLine - 1

                ' Now, we know that the define section begins at i and ends at
                ' curline. Remember where the section begins and ends:

                ReDim Preserve _defineBlocks(_numberSections)
                _defineBlocks(_numberSections) = New DefineBlock
                _defineBlocks(_numberSections).StartLine = i
                _defineBlocks(_numberSections).EndLine = CurLine

                _numberSections = _numberSections + 1
                i = CurLine
            End If
        Next i

        _numberSections = _numberSections - 1

        Dim GotGameBlock As Boolean
        GotGameBlock = False
        For i = 1 To _numberSections
            If BeginsWith(_lines(_defineBlocks(i).StartLine), "define game ") Then
                GotGameBlock = True
                i = _numberSections
            End If
        Next i

        If Not GotGameBlock Then
            _openErrorReport = _openErrorReport & "No 'define game' block." & vbCrLf
            Return False
        End If

        ConvertMultiLineSections()

        bHasErrors = ConvertFriendlyIfs()
        If Not bHasErrors Then bHasErrors = ErrorCheck()

        If bHasErrors Then
            Throw New InvalidOperationException("Errors found in game file.")
            bResult = False
        End If

        _saveGameFile = ""

        Return bResult
    End Function

    Friend Sub LogASLError(TheError As String, Optional MessageType As LogType = LogType.Misc)
        If MessageType = LogType.FatalError Then
            TheError = "FATAL ERROR: " & TheError
        ElseIf MessageType = LogType.WarningError Then
            TheError = "ERROR: " & TheError
        ElseIf MessageType = LogType.LibraryWarningError Then
            TheError = "WARNING ERROR (LIBRARY): " & TheError
        ElseIf MessageType = LogType.Init Then
            TheError = "INIT: " & TheError
        ElseIf MessageType = LogType.Warning Then
            TheError = "WARNING: " & TheError
        ElseIf MessageType = LogType.UserError Then
            TheError = "ERROR (REQUESTED): " & TheError
        ElseIf MessageType = LogType.InternalError Then
            TheError = "INTERNAL ERROR: " & TheError
        End If

        _log.Add(TheError)
    End Sub

    Friend Function RetrieveParameter(InputString As String, ctx As Context, Optional bConvertStringVariables As Boolean = True) As String
        ' Returns the parameters between < and > in a string
        Dim RetrParam As String
        Dim NewParam As String
        Dim StartPos As Integer
        Dim EndPos As Integer

        StartPos = InStr(InputString, "<")
        EndPos = InStr(InputString, ">")

        If StartPos = 0 Or EndPos = 0 Then
            LogASLError("Expected parameter in '" & ReportErrorLine(InputString) & "'", LogType.WarningError)
            Return ""
        End If

        RetrParam = Mid(InputString, StartPos + 1, (EndPos - StartPos) - 1)

        If bConvertStringVariables Then
            If _gameAslVersion >= 320 Then
                NewParam = ConvertParameter(ConvertParameter(ConvertParameter(RetrParam, "#", ConvertType.Strings, ctx), "%", ConvertType.Numeric, ctx), "$", ConvertType.Functions, ctx)
            Else
                If Not Left(RetrParam, 9) = "~Internal" Then
                    NewParam = ConvertParameter(ConvertParameter(ConvertParameter(ConvertParameter(RetrParam, "#", ConvertType.Strings, ctx), "%", ConvertType.Numeric, ctx), "~", ConvertType.Collectables, ctx), "$", ConvertType.Functions, ctx)
                Else
                    NewParam = RetrParam
                End If
            End If
        Else
            NewParam = RetrParam
        End If

        Return EvaluateInlineExpressions(NewParam)
    End Function

    Private Sub AddLine(theline As String)
        'Adds a line to the game script
        Dim NumLines As Integer

        NumLines = UBound(_lines) + 1
        ReDim Preserve _lines(NumLines)
        _lines(NumLines) = theline
    End Sub

    Private Sub LoadCASFile(thefilename As String)
        Dim EndLineReached, ExitTheLoop As Boolean
        Dim TextMode, FinTheLoop As Boolean
        Dim CasVersion As Integer
        Dim StartCat As String = ""
        Dim EndCatPos As Integer
        Dim FileData As String = ""
        Dim ChkVer As String
        Dim j As Integer
        Dim CurLin, TextData As String
        Dim CPos, NextLinePos As Integer
        Dim c, TL, CKW, D As String

        ReDim _lines(0)

        If Config.ReadGameFileFromAzureBlob Then
            Using client As New WebClient
                Dim url As String = thefilename
                Dim baseAddress As Uri = New Uri(url)
                Dim directory As Uri = New Uri(baseAddress, ".")

                Try
                    FileData = client.DownloadString(url)
                Catch ex As WebException
                    url = directory.OriginalString + "_game.cas"
                    FileData = client.DownloadString(url)
                End Try

                Dim parts As String() = directory.OriginalString.Split("/"c)
                m_gameId = parts(parts.Length - 2)
            End Using
        Else
            FileData = System.IO.File.ReadAllText(thefilename, System.Text.Encoding.GetEncoding(1252))
        End If

        ChkVer = Left(FileData, 7)
        If ChkVer = "QCGF001" Then
            CasVersion = 1
        ElseIf ChkVer = "QCGF002" Then
            CasVersion = 2
        ElseIf ChkVer = "QCGF003" Then
            CasVersion = 3
        Else
            Throw New InvalidOperationException("Invalid or corrupted CAS file.")
        End If

        If CasVersion = 3 Then
            StartCat = Keyword2CAS("!startcat")
        End If

        For i As Integer = 9 To Len(FileData)
            If CasVersion = 3 And Mid(FileData, i, 1) = StartCat Then
                ' Read catalog
                _startCatPos = i
                EndCatPos = InStr(j, FileData, Keyword2CAS("!endcat"))
                ReadCatalog(Mid(FileData, j + 1, EndCatPos - j - 1))
                _resourceFile = thefilename
                _resourceOffset = EndCatPos + 1
                i = Len(FileData)
                _fileData = FileData
            Else

                CurLin = ""
                EndLineReached = False
                If TextMode = True Then
                    TextData = Mid(FileData, i, InStr(i, FileData, Chr(253)) - (i - 1))
                    TextData = Left(TextData, Len(TextData) - 1)
                    CPos = 1
                    FinTheLoop = False

                    If TextData <> "" Then

                        Do
                            NextLinePos = InStr(CPos, TextData, Chr(0))
                            If NextLinePos = 0 Then
                                NextLinePos = Len(TextData) + 1
                                FinTheLoop = True
                            End If
                            TL = DecryptString(Mid(TextData, CPos, NextLinePos - CPos))
                            AddLine(TL)
                            CPos = NextLinePos + 1
                        Loop Until FinTheLoop

                    End If

                    TextMode = False
                    i = InStr(i, FileData, Chr(253))
                End If

                j = i
                Do
                    CKW = Mid(FileData, j, 1)
                    c = ConvertCASKeyword(CKW)

                    If c = vbCrLf Then
                        EndLineReached = True
                    Else
                        If Left(c, 1) <> "!" Then
                            CurLin = CurLin & c & " "
                        Else
                            If c = "!quote" Then
                                ExitTheLoop = False
                                CurLin = CurLin & "<"
                                Do
                                    j = j + 1
                                    D = Mid(FileData, j, 1)
                                    If D <> Chr(0) Then
                                        CurLin = CurLin & DecryptString(D)
                                    Else
                                        CurLin = CurLin & "> "
                                        ExitTheLoop = True
                                    End If
                                Loop Until ExitTheLoop
                            ElseIf c = "!unknown" Then
                                ExitTheLoop = False
                                Do
                                    j = j + 1
                                    D = Mid(FileData, j, 1)
                                    If D <> Chr(254) Then
                                        CurLin = CurLin & D
                                    Else
                                        ExitTheLoop = True
                                    End If
                                Loop Until ExitTheLoop
                                CurLin = CurLin & " "
                            End If
                        End If
                    End If

                    j = j + 1
                Loop Until EndLineReached
                AddLine(Trim(CurLin))
                If BeginsWith(CurLin, "define text") Or (CasVersion >= 2 And (BeginsWith(CurLin, "define synonyms") Or BeginsWith(CurLin, "define type") Or BeginsWith(CurLin, "define menu"))) Then
                    TextMode = True
                End If
                'j is already at correct place, but i will be
                'incremented - so put j back one or we will miss a
                'character.
                i = j - 1
            End If
        Next i
    End Sub

    Private Function DecryptString(sString As String) As String
        Dim OS As String
        Dim Z As Integer

        OS = ""
        For Z = 1 To Len(sString)
            Dim v As Byte = System.Text.Encoding.GetEncoding(1252).GetBytes(Mid(sString, Z, 1))(0)
            OS = OS & Chr(v Xor 255)
        Next Z

        Return OS
    End Function

    Private Function RemoveTabs(ConvertLine As String) As String
        Dim foundalltabs As Boolean
        Dim CPos, TabChar As Integer

        If InStr(ConvertLine, Chr(9)) > 0 Then
            'Remove tab characters and change them into
            'spaces; otherwise they bugger up the Trim
            'commands.
            CPos = 1
            foundalltabs = False
            Do
                TabChar = InStr(CPos, ConvertLine, Chr(9))
                If TabChar <> 0 Then
                    ConvertLine = Left(ConvertLine, TabChar - 1) & Space(4) & Mid(ConvertLine, TabChar + 1)
                    CPos = TabChar + 1
                Else
                    foundalltabs = True
                End If
            Loop Until foundalltabs
        End If

        Return ConvertLine
    End Function

    Private Sub DoAddRemove(ChildObjID As Integer, ParentObjID As Integer, DoAdd As Boolean, ctx As Context)

        If DoAdd Then
            AddToObjectProperties("parent=" & _objs(ParentObjID).ObjectName, ChildObjID, ctx)
            _objs(ChildObjID).ContainerRoom = _objs(ParentObjID).ContainerRoom
        Else
            AddToObjectProperties("not parent", ChildObjID, ctx)
        End If

        If _gameAslVersion >= 410 Then
            ' Putting something in a container implicitly makes that
            ' container "seen". Otherwise we could try to "look at" the
            ' object we just put in the container and have disambigution fail!
            AddToObjectProperties("seen", ParentObjID, ctx)
        End If

        UpdateVisibilityInContainers(ctx, _objs(ParentObjID).ObjectName)

    End Sub

    Private Sub DoLook(ObjID As Integer, ctx As Context, Optional ShowExamineError As Boolean = False, Optional ShowDefaultDescription As Boolean = True)

        Dim FoundLook As Boolean
        Dim i As Integer, ErrMsgID As PlayerError
        Dim ObjectContents As String

        FoundLook = False

        ' First, set the "seen" property, and for ASL >= 391, update visibility for any
        ' object that is contained by this object.

        If _gameAslVersion >= 391 Then
            AddToObjectProperties("seen", ObjID, ctx)
            UpdateVisibilityInContainers(ctx, _objs(ObjID).ObjectName)
        End If

        ' First look for action, then look
        ' for property, then check define
        ' section:

        Dim sLookLine As String
        Dim o = _objs(ObjID)

        For i = 1 To o.NumberActions
            If o.Actions(i).ActionName = "look" Then
                FoundLook = True
                ExecuteScript(o.Actions(i).Script, ctx)
                Exit For
            End If
        Next i

        If Not FoundLook Then
            For i = 1 To o.NumberProperties
                If o.Properties(i).PropertyName = "look" Then
                    ' do this odd RetrieveParameter stuff to convert any variables
                    Print(RetrieveParameter("<" & o.Properties(i).PropertyValue & ">", ctx), ctx)
                    FoundLook = True
                    Exit For
                End If
            Next i
        End If

        If Not FoundLook Then
            For i = o.DefinitionSectionStart To o.DefinitionSectionEnd
                If BeginsWith(_lines(i), "look ") Then

                    sLookLine = Trim(GetEverythingAfter(_lines(i), "look "))

                    If Left(sLookLine, 1) = "<" Then
                        Print(RetrieveParameter(_lines(i), ctx), ctx)
                    Else
                        ExecuteScript(sLookLine, ctx, ObjID)
                    End If

                    FoundLook = True
                End If
            Next i
        End If


        If _gameAslVersion >= 391 Then
            ObjectContents = ListContents(ObjID, ctx)
        Else
            ObjectContents = ""
        End If

        If Not FoundLook And ShowDefaultDescription Then

            If ShowExamineError Then
                ErrMsgID = PlayerError.DefaultExamine
            Else
                ErrMsgID = PlayerError.DefaultLook
            End If

            ' Print "Nothing out of the ordinary" or whatever, but only if we're not going to list
            ' any contents.

            If ObjectContents = "" Then PlayerErrorMessage(ErrMsgID, ctx)
        End If

        If ObjectContents <> "" And ObjectContents <> "<script>" Then Print(ObjectContents, ctx)

    End Sub

    Private Sub DoOpenClose(ObjID As Integer, DoOpen As Boolean, ShowLook As Boolean, ctx As Context)

        If DoOpen Then
            AddToObjectProperties("opened", ObjID, ctx)
            If ShowLook Then DoLook(ObjID, ctx, , False)
        Else
            AddToObjectProperties("not opened", ObjID, ctx)
        End If

        UpdateVisibilityInContainers(ctx, _objs(ObjID).ObjectName)

    End Sub

    Private Function EvaluateInlineExpressions(InputLine As String) As String

        ' Evaluates in-line expressions e.g. msg <Hello, did you know that 2 + 2 = {2+2}?>

        If _gameAslVersion < 391 Then
            Return InputLine
        End If

        Dim EndBracePos, BracePos, CurPos As Integer
        Dim ExpResult As ExpressionResult
        Dim Expression, ResultLine As String

        CurPos = 1
        ResultLine = ""

        Do
            BracePos = InStr(CurPos, InputLine, "{")

            If BracePos <> 0 Then

                ResultLine = ResultLine & Mid(InputLine, CurPos, BracePos - CurPos)

                If Mid(InputLine, BracePos, 2) = "{{" Then
                    ' {{ = {
                    CurPos = BracePos + 2
                    ResultLine = ResultLine & "{"
                Else
                    EndBracePos = InStr(BracePos + 1, InputLine, "}")
                    If EndBracePos = 0 Then
                        LogASLError("Expected } in '" & InputLine & "'", LogType.WarningError)
                        Return "<ERROR>"
                    Else
                        Expression = Mid(InputLine, BracePos + 1, EndBracePos - BracePos - 1)
                        ExpResult = ExpressionHandler(Expression)
                        If ExpResult.Success <> ExpressionSuccess.OK Then
                            LogASLError("Error evaluating expression in <" & InputLine & "> - " & ExpResult.Message)
                            Return "<ERROR>"
                        End If

                        ResultLine = ResultLine & ExpResult.Result
                        CurPos = EndBracePos + 1
                    End If
                End If
            Else
                ResultLine = ResultLine & Mid(InputLine, CurPos)
            End If
        Loop Until BracePos = 0 Or CurPos > Len(InputLine)

        ' Above, we only bothered checking for {{. But for consistency, also }} = }. So let's do that:
        CurPos = 1
        Do
            BracePos = InStr(CurPos, ResultLine, "}}")
            If BracePos <> 0 Then
                ResultLine = Left(ResultLine, BracePos) & Mid(ResultLine, BracePos + 2)
                CurPos = BracePos + 1
            End If
        Loop Until BracePos = 0 Or CurPos > Len(ResultLine)

        Return ResultLine
    End Function

    Private Sub ExecAddRemove(CommandLine As String, ctx As Context)
        Dim ChildObjID As Integer
        Dim ChildObjectName As String
        Dim DoAdd As Boolean
        Dim SepPos, ParentObjID, SepLen As Integer
        Dim ParentObjectName As String
        Dim NoParentSpecified As Boolean
        Dim Verb As String = ""
        Dim i As Integer
        Dim Action As String
        Dim FoundAction As Boolean
        Dim ActionScript As String = ""
        Dim PropertyExists As Boolean
        Dim TextToPrint As String
        Dim IsContainer As Boolean
        Dim InventoryPlace As String
        Dim ErrorMsg As String = ""
        Dim GotObject As Boolean
        Dim ChildLength As Integer

        ' handles e.g. PUT CLOCK ON MANTLEPIECE
        '                  ^ child  ^ parent

        InventoryPlace = "inventory"

        NoParentSpecified = False

        If BeginsWith(CommandLine, "put ") Then
            Verb = "put"
            DoAdd = True
            SepPos = InStr(CommandLine, " on ")
            SepLen = 4
            If SepPos = 0 Then
                SepPos = InStr(CommandLine, " in ")
                SepLen = 4
            End If
            If SepPos = 0 Then
                SepPos = InStr(CommandLine, " onto ")
                SepLen = 6
            End If
        ElseIf BeginsWith(CommandLine, "add ") Then
            Verb = "add"
            DoAdd = True
            SepPos = InStr(CommandLine, " to ")
            SepLen = 4
        ElseIf BeginsWith(CommandLine, "remove ") Then
            Verb = "remove"
            DoAdd = False
            SepPos = InStr(CommandLine, " from ")
            SepLen = 6
        End If

        If SepPos = 0 Then
            NoParentSpecified = True
            SepPos = Len(CommandLine) + 1
        End If

        ChildLength = SepPos - (Len(Verb) + 2)

        If ChildLength < 0 Then
            PlayerErrorMessage(PlayerError.BadCommand, ctx)
            _badCmdBefore = Verb
            Exit Sub
        End If

        ChildObjectName = Trim(Mid(CommandLine, Len(Verb) + 2, ChildLength))

        GotObject = False

        If _gameAslVersion >= 392 And DoAdd Then
            ChildObjID = Disambiguate(ChildObjectName, _currentRoom & ";" & InventoryPlace, ctx)

            If ChildObjID > 0 Then
                If _objs(ChildObjID).ContainerRoom = InventoryPlace Then
                    GotObject = True
                Else
                    ' Player is not carrying the object they referred to. So, first take the object.
                    Print("(first taking " & _objs(ChildObjID).Article & ")", ctx)
                    ' Try to take the object
                    ctx.AllowRealNamesInCommand = True
                    ExecCommand("take " & _objs(ChildObjID).ObjectName, ctx, False, , True)

                    If _objs(ChildObjID).ContainerRoom = InventoryPlace Then GotObject = True
                End If

                If Not GotObject Then
                    _badCmdBefore = Verb
                    Exit Sub
                End If
            Else
                If ChildObjID <> -2 Then PlayerErrorMessage(PlayerError.NoItem, ctx)
                _badCmdBefore = Verb
                Exit Sub
            End If

        Else
            ChildObjID = Disambiguate(ChildObjectName, InventoryPlace & ";" & _currentRoom, ctx)

            If ChildObjID <= 0 Then
                If ChildObjID <> -2 Then PlayerErrorMessage(PlayerError.BadThing, ctx)
                _badCmdBefore = Verb
                Exit Sub
            End If
        End If

        If NoParentSpecified And DoAdd Then
            SetStringContents("quest.error.article", _objs(ChildObjID).Article, ctx)
            PlayerErrorMessage(PlayerError.BadPut, ctx)
            Exit Sub
        End If

        If DoAdd Then
            Action = "add"
        Else
            Action = "remove"
        End If

        If Not NoParentSpecified Then
            ParentObjectName = Trim(Mid(CommandLine, SepPos + SepLen))

            ParentObjID = Disambiguate(ParentObjectName, _currentRoom & ";" & InventoryPlace, ctx)

            If ParentObjID <= 0 Then
                If ParentObjID <> -2 Then PlayerErrorMessage(PlayerError.BadThing, ctx)
                _badCmdBefore = Left(CommandLine, SepPos + SepLen)
                Exit Sub
            End If
        Else
            ' Assume the player was referring to the parent that the object is already in,
            ' if it is even in an object already

            If Not IsYes(GetObjectProperty("parent", ChildObjID, True, False)) Then
                PlayerErrorMessage(PlayerError.CantRemove, ctx)
                Exit Sub
            End If

            ParentObjID = GetObjectIDNoAlias(GetObjectProperty("parent", ChildObjID, False, True))
        End If

        ' Check if parent is a container

        IsContainer = IsYes(GetObjectProperty("container", ParentObjID, True, False))

        If Not IsContainer Then
            If DoAdd Then
                PlayerErrorMessage(PlayerError.CantPut, ctx)
            Else
                PlayerErrorMessage(PlayerError.CantRemove, ctx)
            End If
            Exit Sub
        End If

        ' Check object is already held by that parent

        If IsYes(GetObjectProperty("parent", ChildObjID, True, False)) Then
            If DoAdd And LCase(GetObjectProperty("parent", ChildObjID, False, False)) = LCase(_objs(ParentObjID).ObjectName) Then
                PlayerErrorMessage(PlayerError.AlreadyPut, ctx)
            End If
        End If

        ' NEW: Check parent and child are accessible to player
        If Not PlayerCanAccessObject(ChildObjID, , ErrorMsg) Then
            If DoAdd Then
                PlayerErrorMessage_ExtendInfo(PlayerError.CantPut, ctx, ErrorMsg)
            Else
                PlayerErrorMessage_ExtendInfo(PlayerError.CantRemove, ctx, ErrorMsg)
            End If
            Exit Sub
        End If
        If Not PlayerCanAccessObject(ParentObjID, , ErrorMsg) Then
            If DoAdd Then
                PlayerErrorMessage_ExtendInfo(PlayerError.CantPut, ctx, ErrorMsg)
            Else
                PlayerErrorMessage_ExtendInfo(PlayerError.CantRemove, ctx, ErrorMsg)
            End If
            Exit Sub
        End If

        ' Check if parent is a closed container

        If Not IsYes(GetObjectProperty("surface", ParentObjID, True, False)) And Not IsYes(GetObjectProperty("opened", ParentObjID, True, False)) Then
            ' Not a surface and not open, so can't add to this closed container.
            If DoAdd Then
                PlayerErrorMessage(PlayerError.CantPut, ctx)
            Else
                PlayerErrorMessage(PlayerError.CantRemove, ctx)
            End If
            Exit Sub
        End If

        ' Now check if it can be added to (or removed from)

        ' First check for an action
        Dim o = _objs(ParentObjID)
        For i = 1 To o.NumberActions
            If LCase(o.Actions(i).ActionName) = Action Then
                FoundAction = True
                ActionScript = o.Actions(i).Script
                Exit For
            End If
        Next i

        If FoundAction Then
            SetStringContents("quest." & LCase(Action) & ".object.name", _objs(ChildObjID).ObjectName, ctx)
            ExecuteScript(ActionScript, ctx, ParentObjID)
        Else
            ' Now check for a property
            PropertyExists = IsYes(GetObjectProperty(Action, ParentObjID, True, False))

            If Not PropertyExists Then
                ' Show error message
                If DoAdd Then
                    PlayerErrorMessage(PlayerError.CantPut, ctx)
                Else
                    PlayerErrorMessage(PlayerError.CantRemove, ctx)
                End If
            Else
                TextToPrint = GetObjectProperty(Action, ParentObjID, False, False)
                If TextToPrint = "" Then
                    ' Show default message
                    If DoAdd Then
                        PlayerErrorMessage(PlayerError.DefaultPut, ctx)
                    Else
                        PlayerErrorMessage(PlayerError.DefaultRemove, ctx)
                    End If
                Else
                    Print(TextToPrint, ctx)
                End If

                DoAddRemove(ChildObjID, ParentObjID, DoAdd, ctx)

            End If
        End If

    End Sub

    Private Sub ExecAddRemoveScript(Parameter As String, DoAdd As Boolean, ctx As Context)

        Dim ChildObjID, ParentObjID As Integer
        Dim CommandName As String
        Dim ChildName As String
        Dim ParentName As String = ""
        Dim SCP As Integer

        If DoAdd Then
            CommandName = "add"
        Else
            CommandName = "remove"
        End If

        SCP = InStr(Parameter, ";")
        If SCP = 0 And DoAdd Then
            LogASLError("No parent specified in '" & CommandName & " <" & Parameter & ">", LogType.WarningError)
            Exit Sub
        End If

        If SCP <> 0 Then
            ChildName = LCase(Trim(Left(Parameter, SCP - 1)))
            ParentName = LCase(Trim(Mid(Parameter, SCP + 1)))
        Else
            ChildName = LCase(Trim(Parameter))
        End If

        ChildObjID = GetObjectIDNoAlias(ChildName)
        If ChildObjID = 0 Then
            LogASLError("Invalid child object name specified in '" & CommandName & " <" & Parameter & ">", LogType.WarningError)
            Exit Sub
        End If

        If SCP <> 0 Then
            ParentObjID = GetObjectIDNoAlias(ParentName)
            If ParentObjID = 0 Then
                LogASLError("Invalid parent object name specified in '" & CommandName & " <" & Parameter & ">", LogType.WarningError)
                Exit Sub
            End If

            DoAddRemove(ChildObjID, ParentObjID, DoAdd, ctx)
        Else
            AddToObjectProperties("not parent", ChildObjID, ctx)
            UpdateVisibilityInContainers(ctx, _objs(ParentObjID).ObjectName)
        End If

    End Sub

    Private Sub ExecOpenClose(CommandLine As String, ctx As Context)
        Dim ObjID As Integer
        Dim ObjectName As String
        Dim DoOpen As Boolean
        Dim IsOpen, FoundAction As Boolean
        Dim i As Integer
        Dim Action As String = ""
        Dim ActionScript As String = ""
        Dim PropertyExists As Boolean
        Dim TextToPrint As String
        Dim IsContainer As Boolean
        Dim InventoryPlace As String
        Dim ErrorMsg As String = ""

        InventoryPlace = "inventory"

        If BeginsWith(CommandLine, "open ") Then
            Action = "open"
            DoOpen = True
        ElseIf BeginsWith(CommandLine, "close ") Then
            Action = "close"
            DoOpen = False
        End If

        ObjectName = GetEverythingAfter(CommandLine, Action & " ")

        ObjID = Disambiguate(ObjectName, _currentRoom & ";" & InventoryPlace, ctx)

        If ObjID <= 0 Then
            If ObjID <> -2 Then PlayerErrorMessage(PlayerError.BadThing, ctx)
            _badCmdBefore = Action
            Exit Sub
        End If

        ' Check if it's even a container

        IsContainer = IsYes(GetObjectProperty("container", ObjID, True, False))

        If Not IsContainer Then
            If DoOpen Then
                PlayerErrorMessage(PlayerError.CantOpen, ctx)
            Else
                PlayerErrorMessage(PlayerError.CantClose, ctx)
            End If
            Exit Sub
        End If

        ' Check if it's already open (or closed)

        IsOpen = IsYes(GetObjectProperty("opened", ObjID, True, False))

        If DoOpen And IsOpen Then
            ' Object is already open
            PlayerErrorMessage(PlayerError.AlreadyOpen, ctx)
            Exit Sub
        ElseIf Not DoOpen And Not IsOpen Then
            ' Object is already closed
            PlayerErrorMessage(PlayerError.AlreadyClosed, ctx)
            Exit Sub
        End If

        ' NEW: Check if it's accessible, i.e. check it's not itself inside another closed container

        If Not PlayerCanAccessObject(ObjID, , ErrorMsg) Then
            If DoOpen Then
                PlayerErrorMessage_ExtendInfo(PlayerError.CantOpen, ctx, ErrorMsg)
            Else
                PlayerErrorMessage_ExtendInfo(PlayerError.CantClose, ctx, ErrorMsg)
            End If
            Exit Sub
        End If

        ' Now check if it can be opened (or closed)

        ' First check for an action
        Dim o = _objs(ObjID)
        For i = 1 To o.NumberActions
            If LCase(o.Actions(i).ActionName) = Action Then
                FoundAction = True
                ActionScript = o.Actions(i).Script
                Exit For
            End If
        Next i

        If FoundAction Then
            ExecuteScript(ActionScript, ctx, ObjID)
        Else
            ' Now check for a property
            PropertyExists = IsYes(GetObjectProperty(Action, ObjID, True, False))

            If Not PropertyExists Then
                ' Show error message
                If DoOpen Then
                    PlayerErrorMessage(PlayerError.CantOpen, ctx)
                Else
                    PlayerErrorMessage(PlayerError.CantClose, ctx)
                End If
            Else
                TextToPrint = GetObjectProperty(Action, ObjID, False, False)
                If TextToPrint = "" Then
                    ' Show default message
                    If DoOpen Then
                        PlayerErrorMessage(PlayerError.DefaultOpen, ctx)
                    Else
                        PlayerErrorMessage(PlayerError.DefaultClose, ctx)
                    End If
                Else
                    Print(TextToPrint, ctx)
                End If

                DoOpenClose(ObjID, DoOpen, True, ctx)

            End If
        End If

    End Sub

    Private Sub ExecuteSelectCase(ScriptLine As String, ctx As Context)
        Dim CaseBlockName As String
        Dim i As Integer
        Dim CaseBlock As DefineBlock
        Dim AfterLine, ThisCase As String
        Dim SCP As Integer
        Dim CaseMatch As Boolean
        Dim FinLoop As Boolean
        Dim ThisCondition, CheckValue As String
        Dim CaseScript As String = ""

        ' ScriptLine passed will look like this:
        '   select case <whatever> do <!intprocX>
        ' with all the case statements in the intproc.

        AfterLine = GetAfterParameter(ScriptLine)

        If Not BeginsWith(AfterLine, "do <!intproc") Then
            LogASLError("No case block specified for '" & ScriptLine & "'", LogType.WarningError)
            Exit Sub
        End If

        CaseBlockName = RetrieveParameter(AfterLine, ctx)
        CaseBlock = DefineBlockParam("procedure", CaseBlockName)
        CheckValue = RetrieveParameter(ScriptLine, ctx)
        CaseMatch = False

        For i = CaseBlock.StartLine + 1 To CaseBlock.EndLine - 1
            ' Go through all the cases until we find the one that matches

            If _lines(i) <> "" Then
                If Not BeginsWith(_lines(i), "case ") Then
                    LogASLError("Invalid line in 'select case' block: '" & _lines(i) & "'", LogType.WarningError)
                Else

                    If BeginsWith(_lines(i), "case else ") Then
                        CaseMatch = True
                        CaseScript = GetEverythingAfter(_lines(i), "case else ")
                    Else
                        ThisCase = RetrieveParameter(_lines(i), ctx)

                        FinLoop = False

                        Do
                            SCP = InStr(ThisCase, ";")
                            If SCP = 0 Then
                                SCP = Len(ThisCase) + 1
                                FinLoop = True
                            End If

                            ThisCondition = Trim(Left(ThisCase, SCP - 1))
                            If ThisCondition = CheckValue Then
                                CaseScript = GetAfterParameter(_lines(i))
                                CaseMatch = True
                                FinLoop = True
                            Else
                                ThisCase = Mid(ThisCase, SCP + 1)
                            End If
                        Loop Until FinLoop
                    End If

                    If CaseMatch Then
                        ExecuteScript(CaseScript, ctx)
                        Exit For
                    End If
                End If
            End If
        Next i

    End Sub

    Private Function ExecVerb(CommandString As String, ctx As Context, Optional LibCommands As Boolean = False) As Boolean
        Dim gameblock As DefineBlock
        Dim FoundVerb As Boolean
        Dim VerbProperty As String = ""
        Dim Script As String = ""
        Dim VerbsList As String
        Dim ThisVerb As String = ""
        Dim SCP, ColonPos As Integer
        Dim ObjID, i As Integer
        Dim FoundItem, FoundAction As Boolean
        Dim VerbObject As String = ""
        Dim InventoryPlace, VerbTag As String
        Dim ThisScript As String = ""

        FoundVerb = False
        FoundAction = False

        If Not LibCommands Then
            VerbTag = "verb "
        Else
            VerbTag = "lib verb "
        End If

        gameblock = GetDefineBlock("game")
        For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
            If BeginsWith(_lines(i), VerbTag) Then
                VerbsList = RetrieveParameter(_lines(i), ctx)

                ' The property or action the verb uses is either after a colon,
                ' or it's the first (or only) verb on the line.

                ColonPos = InStr(VerbsList, ":")
                If ColonPos <> 0 Then
                    VerbProperty = LCase(Trim(Mid(VerbsList, ColonPos + 1)))
                    VerbsList = Trim(Left(VerbsList, ColonPos - 1))
                Else
                    SCP = InStr(VerbsList, ";")
                    If SCP = 0 Then
                        VerbProperty = LCase(VerbsList)
                    Else
                        VerbProperty = LCase(Trim(Left(VerbsList, SCP - 1)))
                    End If
                End If

                ' Now let's see if this matches:

                Do
                    SCP = InStr(VerbsList, ";")
                    If SCP = 0 Then
                        ThisVerb = LCase(VerbsList)
                    Else
                        ThisVerb = LCase(Trim(Left(VerbsList, SCP - 1)))
                    End If

                    If BeginsWith(CommandString, ThisVerb & " ") Then
                        FoundVerb = True
                        VerbObject = GetEverythingAfter(CommandString, ThisVerb & " ")
                        Script = Trim(Mid(_lines(i), InStr(_lines(i), ">") + 1))
                    End If

                    If SCP <> 0 Then
                        VerbsList = Trim(Mid(VerbsList, SCP + 1))
                    End If
                Loop Until SCP = 0 Or Trim(VerbsList) = "" Or FoundVerb

                If FoundVerb Then Exit For

            End If
        Next i

        If FoundVerb Then

            InventoryPlace = "inventory"

            ObjID = Disambiguate(VerbObject, InventoryPlace & ";" & _currentRoom, ctx)

            If ObjID < 0 Then
                FoundItem = False
            Else
                FoundItem = True
            End If

            If FoundItem = False Then
                If ObjID <> -2 Then PlayerErrorMessage(PlayerError.BadThing, ctx)
                _badCmdBefore = ThisVerb
            Else
                SetStringContents("quest.error.article", _objs(ObjID).Article, ctx)

                ' Now see if this object has the relevant action or property
                Dim o = _objs(ObjID)
                For i = 1 To o.NumberActions
                    If LCase(o.Actions(i).ActionName) = VerbProperty Then
                        FoundAction = True
                        ThisScript = o.Actions(i).Script
                        Exit For
                    End If
                Next i

                If ThisScript <> "" Then
                    ' Avoid an RTE "this array is fixed or temporarily locked"
                    ExecuteScript(ThisScript, ctx, ObjID)
                End If

                If Not FoundAction Then
                    ' Check properties for a message
                    For i = 1 To o.NumberProperties
                        If LCase(o.Properties(i).PropertyName) = VerbProperty Then
                            FoundAction = True
                            Print(o.Properties(i).PropertyValue, ctx)
                            Exit For
                        End If
                    Next i
                End If

                If Not FoundAction Then
                    ' Execute the default script from the verb definition
                    ExecuteScript(Script, ctx)
                End If
            End If
        End If

        Return FoundVerb
    End Function

    Private Function ExpressionHandler(Expression As String) As ExpressionResult

        Dim ObsExp As String
        Dim i As Integer
        Dim Elements() As String
        Dim NumElements As Integer
        Dim Operators(0) As String
        Dim NumOperators As Integer
        Dim OpNum As Integer
        Dim Val2, Val1, Result As Double
        Dim BracketCount, OpenBracketPos, EndBracketPos As Integer
        Dim res As New ExpressionResult
        Dim NestedResult As ExpressionResult
        Dim NewElement As Boolean

        ' Find brackets, recursively call ExpressionHandler
        Do
            OpenBracketPos = InStr(Expression, "(")
            If OpenBracketPos <> 0 Then
                ' Find equivalent closing bracket
                BracketCount = 1
                EndBracketPos = 0
                For i = OpenBracketPos + 1 To Len(Expression)
                    If Mid(Expression, i, 1) = "(" Then
                        BracketCount = BracketCount + 1
                    ElseIf Mid(Expression, i, 1) = ")" Then
                        BracketCount = BracketCount - 1
                    End If

                    If BracketCount = 0 Then
                        EndBracketPos = i
                        Exit For
                    End If
                Next i

                If EndBracketPos <> 0 Then
                    NestedResult = ExpressionHandler(Mid(Expression, OpenBracketPos + 1, EndBracketPos - OpenBracketPos - 1))
                    If NestedResult.Success <> ExpressionSuccess.OK Then
                        res.Success = NestedResult.Success
                        res.Message = NestedResult.Message
                        Return res
                    End If

                    Expression = Left(Expression, OpenBracketPos - 1) & " " & NestedResult.Result & " " & Mid(Expression, EndBracketPos + 1)

                Else
                    res.Message = "Missing closing bracket"
                    res.Success = ExpressionSuccess.Fail
                    Return res

                End If
            End If
        Loop Until OpenBracketPos = 0


        NumElements = 1
        ReDim Elements(1)
        NumOperators = 0

        ' Split expression into elements, e.g.:
        '       2 + 3 * 578.2 / 36
        '       E O E O EEEEE O EE      where E=Element, O=Operator

        ' Populate Elements() and Operators()

        ObsExp = ObscureNumericExps(Expression)

        For i = 1 To Len(Expression)
            Select Case Mid(ObsExp, i, 1)
                Case "+", "*", "/"
                    NewElement = True
                Case "-"
                    ' A minus often means subtraction, so it's a new element. But sometimes
                    ' it just denotes a negative number. In this case, the current element will
                    ' be empty.

                    If Trim(Elements(NumElements)) = "" Then
                        NewElement = False
                    Else
                        NewElement = True
                    End If
                Case Else
                    NewElement = False
            End Select

            If NewElement Then
                NumElements = NumElements + 1
                ReDim Preserve Elements(NumElements)

                NumOperators = NumOperators + 1
                ReDim Preserve Operators(NumOperators)
                Operators(NumOperators) = Mid(Expression, i, 1)
            Else
                Elements(NumElements) = Elements(NumElements) & Mid(Expression, i, 1)
            End If
        Next i

        ' Check Elements are numeric, and trim spaces
        For i = 1 To NumElements

            Elements(i) = Trim(Elements(i))

            If Not IsNumeric(Elements(i)) Then
                res.Message = "Syntax error evaluating expression - non-numeric element '" & Elements(i) & "'"
                res.Success = ExpressionSuccess.Fail
                Return res
            End If
        Next i

        Do
            ' Go through the Operators array to find next calculation to perform

            OpNum = 0

            For i = 1 To NumOperators
                If Operators(i) = "/" Or Operators(i) = "*" Then
                    OpNum = i
                    Exit For
                End If
            Next i

            If OpNum = 0 Then
                For i = 1 To NumOperators
                    If Operators(i) = "+" Or Operators(i) = "-" Then
                        OpNum = i
                        Exit For
                    End If
                Next i
            End If

            ' If OpNum is still 0, there are no calculations left to do.

            If OpNum <> 0 Then

                Val1 = CDbl(Elements(OpNum))
                Val2 = CDbl(Elements(OpNum + 1))

                Select Case Operators(OpNum)
                    Case "/"
                        If Val2 = 0 Then
                            res.Message = "Division by zero"
                            res.Success = ExpressionSuccess.Fail
                            Return res
                        End If
                        Result = Val1 / Val2
                    Case "*"
                        Result = Val1 * Val2
                    Case "+"
                        Result = Val1 + Val2
                    Case "-"
                        Result = Val1 - Val2
                End Select

                Elements(OpNum) = CStr(Result)

                ' Remove this operator, and Elements(OpNum+1) from the arrays
                For i = OpNum To NumOperators - 1
                    Operators(i) = Operators(i + 1)
                Next i
                For i = OpNum + 1 To NumElements - 1
                    Elements(i) = Elements(i + 1)
                Next i
                NumOperators = NumOperators - 1
                NumElements = NumElements - 1
                ReDim Preserve Operators(NumOperators)
                ReDim Preserve Elements(NumElements)

            End If
        Loop Until OpNum = 0 Or NumOperators = 0

        res.Success = ExpressionSuccess.OK
        res.Result = Elements(1)
        Return res

    End Function

    Private Function ListContents(ObjID As Integer, ctx As Context) As String

        ' Returns a formatted list of the contents of a container.
        ' If the list action causes a script to be run instead, ListContents
        ' returns "<script>"

        Dim Contents As String
        Dim i, NumContents As Integer
        Dim ContentsIDs(0) As Integer
        Dim ListString As String
        Dim DisplayList As Boolean

        If Not IsYes(GetObjectProperty("container", ObjID, True, False)) Then
            Return ""
        End If

        If Not IsYes(GetObjectProperty("opened", ObjID, True, False)) And Not IsYes(GetObjectProperty("transparent", ObjID, True, False)) And Not IsYes(GetObjectProperty("surface", ObjID, True, False)) Then
            ' Container is closed, so return "list closed" property if there is one.

            If DoAction(ObjID, "list closed", ctx, False) Then
                Return "<script>"
            Else
                Return GetObjectProperty("list closed", ObjID, False, False)
            End If
        End If

        ' populate contents string

        NumContents = 0

        For i = 1 To _numberObjs
            If _objs(i).Exists And _objs(i).Visible Then
                If LCase(GetObjectProperty("parent", i, False, False)) = LCase(_objs(ObjID).ObjectName) Then
                    NumContents = NumContents + 1
                    ReDim Preserve ContentsIDs(NumContents)
                    ContentsIDs(NumContents) = i
                End If
            End If
        Next i

        Contents = ""

        If NumContents > 0 Then
            ' Check if list property is set.

            If DoAction(ObjID, "list", ctx, False) Then
                Return "<script>"
            End If

            If IsYes(GetObjectProperty("list", ObjID, True, False)) Then
                ' Read header, if any
                ListString = GetObjectProperty("list", ObjID, False, False)

                DisplayList = True

                If ListString <> "" Then
                    If Right(ListString, 1) = ":" Then
                        Contents = Left(ListString, Len(ListString) - 1) & " "
                    Else
                        ' If header doesn't end in a colon, then the header is the only text to print
                        Contents = ListString
                        DisplayList = False
                    End If
                Else
                    Contents = UCase(Left(_objs(ObjID).Article, 1)) & Mid(_objs(ObjID).Article, 2) & " contains "
                End If

                If DisplayList Then
                    For i = 1 To NumContents
                        If i > 1 Then
                            If i < NumContents Then
                                Contents = Contents & ", "
                            Else
                                Contents = Contents & " and "
                            End If
                        End If

                        Dim o = _objs(ContentsIDs(i))
                        If o.Prefix <> "" Then Contents = Contents & o.Prefix
                        If o.ObjectAlias <> "" Then
                            Contents = Contents & "|b" & o.ObjectAlias & "|xb"
                        Else
                            Contents = Contents & "|b" & o.ObjectName & "|xb"
                        End If
                        If o.Suffix <> "" Then Contents = Contents & " " & o.Suffix
                    Next i
                End If

                Return Contents & "."
            End If
            ' The "list" property is not set, so do not list contents.
            Return ""
        End If

        ' Container is empty, so return "list empty" property if there is one.

        If DoAction(ObjID, "list empty", ctx, False) Then
            Return "<script>"
        Else
            Return GetObjectProperty("list empty", ObjID, False, False)
        End If

    End Function

    Private Function ObscureNumericExps(InputString As String) As String

        ' Obscures + or - next to E in Double-type variables with exponents
        '   e.g. 2.345E+20 becomes 2.345EX20
        ' This stops huge numbers breaking parsing of maths functions

        Dim EPos, CurPos As Integer
        Dim OutputString As String
        OutputString = InputString

        CurPos = 1
        Do
            EPos = InStr(CurPos, OutputString, "E")
            If EPos <> 0 Then
                OutputString = Left(OutputString, EPos) & "X" & Mid(OutputString, EPos + 2)
                CurPos = EPos + 2
            End If
        Loop Until EPos = 0

        Return OutputString
    End Function

    Private Sub ProcessListInfo(ListLine As String, ObjID As Integer)
        Dim ListInfo As New TextAction
        Dim PropName As String = ""

        If BeginsWith(ListLine, "list closed <") Then
            ListInfo.Type = TextActionType.Text
            ListInfo.Data = RetrieveParameter(ListLine, _nullContext)
            PropName = "list closed"
        ElseIf Trim(ListLine) = "list closed off" Then
            ' default for list closed is off anyway
            Exit Sub
        ElseIf BeginsWith(ListLine, "list closed") Then
            ListInfo.Type = TextActionType.Script
            ListInfo.Data = GetEverythingAfter(ListLine, "list closed")
            PropName = "list closed"


        ElseIf BeginsWith(ListLine, "list empty <") Then
            ListInfo.Type = TextActionType.Text
            ListInfo.Data = RetrieveParameter(ListLine, _nullContext)
            PropName = "list empty"
        ElseIf Trim(ListLine) = "list empty off" Then
            ' default for list empty is off anyway
            Exit Sub
        ElseIf BeginsWith(ListLine, "list empty") Then
            ListInfo.Type = TextActionType.Script
            ListInfo.Data = GetEverythingAfter(ListLine, "list empty")
            PropName = "list empty"


        ElseIf Trim(ListLine) = "list off" Then
            AddToObjectProperties("not list", ObjID, _nullContext)
            Exit Sub
        ElseIf BeginsWith(ListLine, "list <") Then
            ListInfo.Type = TextActionType.Text
            ListInfo.Data = RetrieveParameter(ListLine, _nullContext)
            PropName = "list"
        ElseIf BeginsWith(ListLine, "list ") Then
            ListInfo.Type = TextActionType.Script
            ListInfo.Data = GetEverythingAfter(ListLine, "list ")
            PropName = "list"
        End If

        If PropName <> "" Then
            If ListInfo.Type = TextActionType.Text Then
                AddToObjectProperties(PropName & "=" & ListInfo.Data, ObjID, _nullContext)
            Else
                AddToObjectActions("<" & PropName & "> " & ListInfo.Data, ObjID, _nullContext)
            End If
        End If
    End Sub

    Private Function GetHTMLColour(Colour As String, DefaultColour As String) As String
        ' Converts a Quest foreground or background colour setting into an HTML colour

        Colour = LCase(Colour)

        If Colour = "" Or Colour = "0" Then Colour = DefaultColour

        Select Case Colour
            Case "white"
                Return "FFFFFF"
            Case "black"
                Return "000000"
            Case "blue"
                Return "0000FF"
            Case "yellow"
                Return "FFFF00"
            Case "red"
                Return "FF0000"
            Case "green"
                Return "00FF00"
            Case Else
                Return Colour
        End Select

        Return ""
    End Function

    Private Sub DoPrint(OutputText As String)
        RaiseEvent PrintText(_textFormatter.OutputHTML(OutputText))
    End Sub

    Private Sub DestroyExit(ExitData As String, ctx As Context)
        Dim SCP As Integer
        Dim FoundID As Boolean
        Dim FromRoom As String = ""
        Dim ToRoom As String = ""
        Dim RoomID, i, ExitID As Integer
        Dim FoundExit As Boolean

        SCP = InStr(ExitData, ";")
        If SCP = 0 Then
            LogASLError("No exit name specified in 'destroy exit <" & ExitData & ">'")
            Exit Sub
        End If

        Dim oExit As RoomExit
        If _gameAslVersion >= 410 Then
            oExit = FindExit(ExitData)
            If oExit Is Nothing Then
                LogASLError("Can't find exit in 'destroy exit <" & ExitData & ">'")
                Exit Sub
            End If

            oExit.Parent.RemoveExit(oExit)

        Else

            FromRoom = LCase(Trim(Left(ExitData, SCP - 1)))
            ToRoom = Trim(Mid(ExitData, SCP + 1))

            ' Find From Room:
            FoundID = False

            For i = 1 To _numberRooms
                If LCase(_rooms(i).RoomName) = FromRoom Then
                    FoundID = True
                    RoomID = i
                    i = _numberRooms
                End If
            Next i

            If Not FoundID Then
                LogASLError("No such room '" & FromRoom & "'")
                Exit Sub
            End If

            FoundExit = False
            Dim r = _rooms(RoomID)

            For i = 1 To r.NumberPlaces
                If r.Places(i).PlaceName = ToRoom Then
                    ExitID = i
                    FoundExit = True
                    i = r.NumberPlaces
                End If
            Next i

            If FoundExit Then
                For i = ExitID To r.NumberPlaces - 1
                    r.Places(i) = r.Places(i + 1)
                Next i
                ReDim Preserve r.Places(r.NumberPlaces - 1)
                r.NumberPlaces = r.NumberPlaces - 1
            End If
        End If

        ' Update quest.* vars and obj list
        ShowRoomInfo(_currentRoom, ctx, True)
        UpdateObjectList(ctx)

        AddToChangeLog("room " & FromRoom, "destroy exit " & ToRoom)

    End Sub

    Private Sub DoClear()
        m_player.ClearScreen()
    End Sub

    Private Sub DoWait()

        m_player.DoWait()
        ChangeState(State.Waiting)

        SyncLock _waitLock
            System.Threading.Monitor.Wait(_waitLock)
        End SyncLock

    End Sub

    Private Sub ExecuteFlag(InputLine As String, ctx As Context)
        Dim PropertyString As String = ""

        If BeginsWith(InputLine, "on ") Then
            PropertyString = RetrieveParameter(InputLine, ctx)
        ElseIf BeginsWith(InputLine, "off ") Then
            PropertyString = "not " & RetrieveParameter(InputLine, ctx)
        End If

        ' Game object always has ObjID 1
        AddToObjectProperties(PropertyString, 1, ctx)

    End Sub

    Private Function ExecuteIfFlag(flag As String) As Boolean
        ' Game ObjID is 1
        Return GetObjectProperty(flag, 1, True) = "yes"
    End Function

    Private Sub ExecuteIncDec(InputLine As String, ctx As Context)
        Dim SC As Integer
        Dim Param, Var As String
        Dim Change As Double
        Dim Value As Double
        Dim ArrayIndex As Integer
        Param = RetrieveParameter(InputLine, ctx)

        SC = InStr(Param, ";")
        If SC = 0 Then
            Change = 1
            Var = Param
        Else
            Change = Val(Mid(Param, SC + 1))
            Var = Trim(Left(Param, SC - 1))
        End If

        Value = GetNumericContents(Var, ctx, True)
        If Value <= -32766 Then Value = 0

        If BeginsWith(InputLine, "inc ") Then
            Value = Value + Change
        ElseIf BeginsWith(InputLine, "dec ") Then
            Value = Value - Change
        End If

        ArrayIndex = GetArrayIndex(Var, ctx)

        SetNumericVariableContents(Var, Value, ctx, ArrayIndex)

    End Sub

    Private Function ExtractFile(FileToExtract As String) As String
        Dim Length, StartPos, i As Integer
        Dim FoundRes As Boolean
        Dim FileData As String
        Dim Extracted As Boolean
        Dim ResID As Integer
        Dim sFileName As String

        If _resourceFile = "" Then Return ""

        ' Find file in catalog

        FoundRes = False

        For i = 1 To _numResources
            If LCase(FileToExtract) = LCase(_resources(i).ResourceName) Then
                FoundRes = True
                StartPos = _resources(i).ResourceStart + _resourceOffset
                Length = _resources(i).ResourceLength
                Extracted = _resources(i).Extracted
                ResID = i
                Exit For
            End If
        Next i

        If Not FoundRes Then
            LogASLError("Unable to extract '" & FileToExtract & "' - not present in resources.", LogType.WarningError)
            Return Nothing
        End If

        sFileName = System.IO.Path.Combine(_tempFolder, FileToExtract)
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sFileName))

        If Not Extracted Then
            ' Extract file from cached CAS data
            FileData = Mid(_fileData, StartPos, Length)

            ' Write file to temp dir
            System.IO.File.WriteAllText(sFileName, FileData, System.Text.Encoding.GetEncoding(1252))

            _resources(ResID).Extracted = True
        End If

        Return sFileName

    End Function

    Private Sub AddObjectAction(ObjID As Integer, name As String, script As String, Optional NoUpdate As Boolean = False)

        ' Use NoUpdate in e.g. AddToGiveInfo, otherwise ObjectActionUpdate will call
        ' AddToGiveInfo again leading to a big loop

        Dim FoundExisting As Boolean
        Dim ActionNum, i As Integer
        FoundExisting = False

        Dim o = _objs(ObjID)

        For i = 1 To o.NumberActions
            If o.Actions(i).ActionName = name Then
                FoundExisting = True
                ActionNum = i
                i = o.NumberActions
            End If
        Next i

        If Not FoundExisting Then
            o.NumberActions = o.NumberActions + 1
            ReDim Preserve o.Actions(o.NumberActions)
            o.Actions(o.NumberActions) = New ActionType
            ActionNum = o.NumberActions
        End If

        o.Actions(ActionNum).ActionName = name
        o.Actions(ActionNum).Script = script

        ObjectActionUpdate(ObjID, name, script, NoUpdate)

    End Sub

    Private Sub AddToChangeLog(AppliesTo As String, ChangeData As String)
        _gameChangeData.NumberChanges = _gameChangeData.NumberChanges + 1
        ReDim Preserve _gameChangeData.ChangeData(_gameChangeData.NumberChanges)
        _gameChangeData.ChangeData(_gameChangeData.NumberChanges) = New ChangeType
        _gameChangeData.ChangeData(_gameChangeData.NumberChanges).AppliesTo = AppliesTo
        _gameChangeData.ChangeData(_gameChangeData.NumberChanges).Change = ChangeData
    End Sub

    Private Sub AddToOOChangeLog(lAppliesToType As ChangeLog.eAppliesToType, sAppliesTo As String, sElement As String, sChangeData As String)

        Dim oChangeLog As ChangeLog

        ' NOTE: We're only actually ever using the object changelog at the moment.
        ' Rooms only get logged for creating rooms and creating/destroying exits, so we don't
        ' need the refactored ChangeLog component for those - although at some point the
        ' QSG code should be refactored so we don't have two different types of change logs.

        Select Case lAppliesToType
            Case ChangeLog.eAppliesToType.atObject
                oChangeLog = _changeLogObjects
            Case ChangeLog.eAppliesToType.atRoom
                oChangeLog = _changeLogRooms
            Case Else
                Throw New ArgumentOutOfRangeException()
        End Select

        oChangeLog.AddItem(sAppliesTo, sElement, sChangeData)

        Exit Sub

    End Sub

    Private Sub AddToGiveInfo(ObjID As Integer, GiveData As String)
        Dim ObjectName As String
        Dim DataID As Integer
        Dim Found As Boolean
        Dim EP As Integer
        Dim CurGiveType As GiveType, i As Integer
        Dim actionName As String
        Dim actionScript As String

        Dim o = _objs(ObjID)

        If BeginsWith(GiveData, "to ") Then
            GiveData = GetEverythingAfter(GiveData, "to ")
            If BeginsWith(GiveData, "anything ") Then
                o.GiveToAnything = GetEverythingAfter(GiveData, "anything ")

                AddObjectAction(ObjID, "give to anything", o.GiveToAnything, True)
                Exit Sub
            Else
                CurGiveType = GiveType.GiveToSomething

                actionName = "give to "
            End If
        Else
            If BeginsWith(GiveData, "anything ") Then
                o.GiveAnything = GetEverythingAfter(GiveData, "anything ")

                AddObjectAction(ObjID, "give anything", o.GiveAnything, True)
                Exit Sub
            Else
                CurGiveType = GiveType.GiveSomethingTo
                actionName = "give "
            End If
        End If

        If Left(Trim(GiveData), 1) = "<" Then
            ObjectName = RetrieveParameter(GiveData, _nullContext)

            actionName = actionName & "'" & ObjectName & "'"

            Found = False
            For i = 1 To o.NumberGiveData
                If o.GiveData(i).GiveType = CurGiveType And LCase(o.GiveData(i).GiveObject) = LCase(ObjectName) Then
                    DataID = i
                    i = o.NumberGiveData
                    Found = True
                End If
            Next i

            If Not Found Then
                o.NumberGiveData = o.NumberGiveData + 1
                ReDim Preserve o.GiveData(o.NumberGiveData)
                o.GiveData(o.NumberGiveData) = New GiveDataType
                DataID = o.NumberGiveData
            End If

            EP = InStr(GiveData, ">")
            o.GiveData(DataID).GiveType = CurGiveType
            o.GiveData(DataID).GiveObject = ObjectName
            o.GiveData(DataID).GiveScript = Mid(GiveData, EP + 2)

            actionScript = o.GiveData(DataID).GiveScript
            AddObjectAction(ObjID, actionName, actionScript, True)

        End If

    End Sub

    Friend Sub AddToObjectActions(ActionInfo As String, ObjID As Integer, ctx As Context)
        Dim FoundExisting As Boolean
        Dim ActionName As String
        Dim ActionScript As String
        Dim EP As Integer
        Dim ActionNum, i As Integer
        FoundExisting = False

        ActionName = LCase(RetrieveParameter(ActionInfo, ctx))
        EP = InStr(ActionInfo, ">")
        If EP = Len(ActionInfo) Then
            LogASLError("No script given for '" & ActionName & "' action data", LogType.WarningError)
            Exit Sub
        End If

        ActionScript = Trim(Mid(ActionInfo, EP + 1))

        Dim o = _objs(ObjID)

        For i = 1 To o.NumberActions
            If o.Actions(i).ActionName = ActionName Then
                FoundExisting = True
                ActionNum = i
                i = o.NumberActions
            End If
        Next i

        If Not FoundExisting Then
            o.NumberActions = o.NumberActions + 1
            ReDim Preserve o.Actions(o.NumberActions)
            o.Actions(o.NumberActions) = New ActionType
            ActionNum = o.NumberActions
        End If

        o.Actions(ActionNum).ActionName = ActionName
        o.Actions(ActionNum).Script = ActionScript

        ObjectActionUpdate(ObjID, ActionName, ActionScript)
    End Sub

    Private Sub AddToObjectAltNames(AltNames As String, ObjID As Integer)
        Dim EndPos As Integer
        Dim CurName As String

        Dim o = _objs(ObjID)

        Do
            EndPos = InStr(AltNames, ";")
            If EndPos = 0 Then EndPos = Len(AltNames) + 1
            CurName = Trim(Left(AltNames, EndPos - 1))

            If CurName <> "" Then
                o.NumberAltNames = o.NumberAltNames + 1
                ReDim Preserve o.AltNames(o.NumberAltNames)
                o.AltNames(o.NumberAltNames) = CurName
            End If

            AltNames = Mid(AltNames, EndPos + 1)
        Loop Until Trim(AltNames) = ""
    End Sub

    Friend Sub AddToObjectProperties(PropertyInfo As String, ObjID As Integer, ctx As Context)
        If ObjID = 0 Then Return

        Dim CurInfo As String
        Dim SCP, EP As Integer
        Dim CurName, CurValue As String
        Dim CurNum As Integer
        Dim Found As Boolean
        Dim FalseProperty As Boolean
        Dim i As Integer

        If Right(PropertyInfo, 1) <> ";" Then
            PropertyInfo = PropertyInfo & ";"
        End If

        Do
            SCP = InStr(PropertyInfo, ";")
            CurInfo = Left(PropertyInfo, SCP - 1)
            PropertyInfo = Trim(Mid(PropertyInfo, SCP + 1))

            If CurInfo = "" Then Exit Do

            EP = InStr(CurInfo, "=")
            If EP <> 0 Then
                CurName = Trim(Left(CurInfo, EP - 1))
                CurValue = Trim(Mid(CurInfo, EP + 1))
            Else
                CurName = CurInfo
                CurValue = ""
            End If

            FalseProperty = False
            If BeginsWith(CurName, "not ") And CurValue = "" Then
                FalseProperty = True
                CurName = GetEverythingAfter(CurName, "not ")
            End If

            Dim o = _objs(ObjID)

            Found = False
            For i = 1 To o.NumberProperties
                If LCase(o.Properties(i).PropertyName) = LCase(CurName) Then
                    Found = True
                    CurNum = i
                    i = o.NumberProperties
                End If
            Next i

            If Not Found Then
                o.NumberProperties = o.NumberProperties + 1
                ReDim Preserve o.Properties(o.NumberProperties)
                o.Properties(o.NumberProperties) = New PropertyType
                CurNum = o.NumberProperties
            End If

            If FalseProperty Then
                o.Properties(CurNum).PropertyName = ""
            Else
                o.Properties(CurNum).PropertyName = CurName
                o.Properties(CurNum).PropertyValue = CurValue
            End If

            AddToOOChangeLog(ChangeLog.eAppliesToType.atObject, _objs(ObjID).ObjectName, CurName, "properties " & CurInfo)

            Select Case CurName
                Case "alias"
                    If o.IsRoom Then
                        _rooms(o.CorresRoomId).RoomAlias = CurValue
                    Else
                        o.ObjectAlias = CurValue
                    End If
                    If _gameFullyLoaded Then
                        UpdateObjectList(ctx)
                        UpdateItems(ctx)
                    End If
                Case "prefix"
                    If o.IsRoom Then
                        _rooms(o.CorresRoomId).Prefix = CurValue
                    Else
                        If CurValue <> "" Then
                            o.Prefix = CurValue & " "
                        Else
                            o.Prefix = ""
                        End If
                    End If
                Case "indescription"
                    If o.IsRoom Then _rooms(o.CorresRoomId).InDescription = CurValue
                Case "description"
                    If o.IsRoom Then
                        _rooms(o.CorresRoomId).Description.Data = CurValue
                        _rooms(o.CorresRoomId).Description.Type = TextActionType.Text
                    End If
                Case "look"
                    If o.IsRoom Then
                        _rooms(o.CorresRoomId).Look = CurValue
                    End If
                Case "suffix"
                    o.Suffix = CurValue
                Case "displaytype"
                    o.DisplayType = CurValue
                    If _gameFullyLoaded Then UpdateObjectList(ctx)
                Case "gender"
                    o.Gender = CurValue
                Case "article"
                    o.Article = CurValue
                Case "detail"
                    o.Detail = CurValue
                Case "hidden"
                    If FalseProperty Then
                        o.Exists = True
                    Else
                        o.Exists = False
                    End If

                    If _gameFullyLoaded Then UpdateObjectList(ctx)
                Case "invisible"
                    If FalseProperty Then
                        o.Visible = True
                    Else
                        o.Visible = False
                    End If

                    If _gameFullyLoaded Then UpdateObjectList(ctx)
                Case "take"
                    If _gameAslVersion >= 392 Then
                        If FalseProperty Then
                            o.Take.Type = TextActionType.Nothing
                        Else
                            If CurValue = "" Then
                                o.Take.Type = TextActionType.Default
                            Else
                                o.Take.Type = TextActionType.Text
                                o.Take.Data = CurValue
                            End If
                        End If
                    End If
            End Select
        Loop Until Len(Trim(PropertyInfo)) = 0

    End Sub

    Private Sub AddToUseInfo(ObjID As Integer, UseData As String)
        Dim ObjectName As String
        Dim DataID As Integer
        Dim Found As Boolean
        Dim EP As Integer
        Dim CurUseType As UseType, i As Integer

        Dim o = _objs(ObjID)

        If BeginsWith(UseData, "on ") Then
            UseData = GetEverythingAfter(UseData, "on ")
            If BeginsWith(UseData, "anything ") Then
                o.UseOnAnything = GetEverythingAfter(UseData, "anything ")
                Exit Sub
            Else
                CurUseType = UseType.UseOnSomething
            End If
        Else
            If BeginsWith(UseData, "anything ") Then
                o.UseAnything = GetEverythingAfter(UseData, "anything ")
                Exit Sub
            Else
                CurUseType = UseType.UseSomethingOn
            End If
        End If

        If Left(Trim(UseData), 1) = "<" Then
            ObjectName = RetrieveParameter(UseData, _nullContext)
            Found = False
            For i = 1 To o.NumberUseData
                If o.UseData(i).UseType = CurUseType And LCase(o.UseData(i).UseObject) = LCase(ObjectName) Then
                    DataID = i
                    i = o.NumberUseData
                    Found = True
                End If
            Next i

            If Not Found Then
                o.NumberUseData = o.NumberUseData + 1
                ReDim Preserve o.UseData(o.NumberUseData)
                o.UseData(o.NumberUseData) = New UseDataType
                DataID = o.NumberUseData
            End If

            EP = InStr(UseData, ">")
            o.UseData(DataID).UseType = CurUseType
            o.UseData(DataID).UseObject = ObjectName
            o.UseData(DataID).UseScript = Mid(UseData, EP + 2)
        Else
            o.Use = Trim(UseData)
        End If

    End Sub

    Private Function CapFirst(InputString As String) As String
        Return UCase(Left(InputString, 1)) & Mid(InputString, 2)
    End Function

    Private Function ConvertVarsIn(InputString As String, ctx As Context) As String
        Return RetrieveParameter("<" & InputString & ">", ctx)
    End Function

    Private Function DisambObjHere(ctx As Context, ObjID As Integer, FirstPlace As String, Optional TwoPlaces As Boolean = False, Optional SecondPlace As String = "", Optional bExit As Boolean = False) As Boolean

        Dim OnlySeen, ObjIsSeen As Boolean
        Dim RoomObjID As Integer
        Dim InventoryPlace As String
        OnlySeen = False

        If FirstPlace = "game" Then
            FirstPlace = ""
            If SecondPlace = "seen" Then
                TwoPlaces = False
                SecondPlace = ""
                OnlySeen = True
                RoomObjID = _rooms(GetRoomID(_objs(ObjID).ContainerRoom, ctx)).ObjId

                InventoryPlace = "inventory"

                If _objs(ObjID).ContainerRoom = InventoryPlace Then
                    ObjIsSeen = True
                Else
                    If IsYes(GetObjectProperty("visited", RoomObjID, True, False)) Then
                        ObjIsSeen = True
                    Else
                        If IsYes(GetObjectProperty("seen", ObjID, True, False)) Then
                            ObjIsSeen = True
                        End If
                    End If
                End If

            End If
        End If

        If ((TwoPlaces = False And (LCase(_objs(ObjID).ContainerRoom) = LCase(FirstPlace) Or FirstPlace = "")) Or (TwoPlaces = True And (LCase(_objs(ObjID).ContainerRoom) = LCase(FirstPlace) Or LCase(_objs(ObjID).ContainerRoom) = LCase(SecondPlace)))) And _objs(ObjID).Exists = True And _objs(ObjID).IsExit = bExit Then
            If Not OnlySeen Then
                Return True
            End If
            Return ObjIsSeen
        End If

        Return False
    End Function

    Private Sub ExecClone(CloneString As String, ctx As Context)
        Dim SC2, SC, ObjID As Integer
        Dim NewObjName, ObjToClone, CloneTo As String

        SC = InStr(CloneString, ";")
        If SC = 0 Then
            LogASLError("No new object name specified in 'clone <" & CloneString & ">", LogType.WarningError)
            Exit Sub
        Else
            ObjToClone = Trim(Left(CloneString, SC - 1))
            ObjID = GetObjectIDNoAlias(ObjToClone)

            SC2 = InStr(SC + 1, CloneString, ";")
            If SC2 = 0 Then
                CloneTo = _objs(ObjID).ContainerRoom
                NewObjName = Trim(Mid(CloneString, SC + 1))
            Else
                CloneTo = Trim(Mid(CloneString, SC2 + 1))
                NewObjName = Trim(Mid(CloneString, SC + 1, (SC2 - SC) - 1))
            End If
        End If

        _numberObjs = _numberObjs + 1
        ReDim Preserve _objs(_numberObjs)
        _objs(_numberObjs) = New ObjectType
        _objs(_numberObjs) = _objs(ObjID)
        _objs(_numberObjs).ContainerRoom = CloneTo
        _objs(_numberObjs).ObjectName = NewObjName

        If _objs(ObjID).IsRoom Then
            ' This is a room so create the corresponding room as well

            _numberRooms = _numberRooms + 1
            ReDim Preserve _rooms(_numberRooms)
            _rooms(_numberRooms) = New RoomType
            _rooms(_numberRooms) = _rooms(_objs(ObjID).CorresRoomId)
            _rooms(_numberRooms).RoomName = NewObjName
            _rooms(_numberRooms).ObjId = _numberObjs

            _objs(_numberObjs).CorresRoom = NewObjName
            _objs(_numberObjs).CorresRoomId = _numberRooms

            AddToChangeLog("room " & NewObjName, "create")
        Else
            AddToChangeLog("object " & NewObjName, "create " & _objs(_numberObjs).ContainerRoom)
        End If

        UpdateObjectList(ctx)


    End Sub

    Private Sub ExecOops(Correction As String, ctx As Context)

        If _badCmdBefore <> "" Then
            If _badCmdAfter = "" Then
                ExecCommand(_badCmdBefore & " " & Correction, ctx, False)
            Else
                ExecCommand(_badCmdBefore & " " & Correction & " " & _badCmdAfter, ctx, False)
            End If
        End If

    End Sub

    Private Sub ExecType(TypeData As String, ctx As Context)
        Dim SCP As Integer
        Dim ObjName, TypeName As String
        Dim ObjID As Integer
        Dim Found As Boolean
        Dim PropertyData As PropertiesActions
        Dim i As Integer
        SCP = InStr(TypeData, ";")

        If SCP = 0 Then
            LogASLError("No type name given in 'type <" & TypeData & ">'")
            Exit Sub
        End If

        ObjName = Trim(Left(TypeData, SCP - 1))
        TypeName = Trim(Mid(TypeData, SCP + 1))

        For i = 1 To _numberObjs
            If LCase(_objs(i).ObjectName) = LCase(ObjName) Then
                Found = True
                ObjID = i
                i = _numberObjs
            End If
        Next i

        If Not Found Then
            LogASLError("No such object in 'type <" & TypeData & ">'")
            Exit Sub
        End If

        Dim o = _objs(ObjID)

        o.NumberTypesIncluded = o.NumberTypesIncluded + 1
        ReDim Preserve o.TypesIncluded(o.NumberTypesIncluded)
        o.TypesIncluded(o.NumberTypesIncluded) = TypeName

        PropertyData = GetPropertiesInType(TypeName)
        AddToObjectProperties(PropertyData.Properties, ObjID, ctx)
        For i = 1 To PropertyData.NumberActions
            AddObjectAction(ObjID, PropertyData.Actions(i).ActionName, PropertyData.Actions(i).Script)
        Next i

        ' New as of Quest 4.0. Fixes bug that "if type" would fail for any
        ' parent types included by the "type" command.
        For i = 1 To PropertyData.NumberTypesIncluded
            o.NumberTypesIncluded = o.NumberTypesIncluded + 1
            ReDim Preserve o.TypesIncluded(o.NumberTypesIncluded)
            o.TypesIncluded(o.NumberTypesIncluded) = PropertyData.TypesIncluded(i)
        Next i
    End Sub

    Private Function ExecuteIfAction(ActionData As String) As Boolean
        Dim SCP, ObjID As Integer
        Dim ObjName As String
        Dim ActionName As String
        Dim FoundObj As Boolean
        Dim Result As Boolean
        Dim i As Integer

        SCP = InStr(ActionData, ";")

        If SCP = 0 Then
            LogASLError("No action name given in condition 'action <" & ActionData & ">' ...", LogType.WarningError)
            Return False
        End If

        ObjName = Trim(Left(ActionData, SCP - 1))
        ActionName = Trim(Mid(ActionData, SCP + 1))

        FoundObj = False

        For i = 1 To _numberObjs
            If LCase(_objs(i).ObjectName) = LCase(ObjName) Then
                FoundObj = True
                ObjID = i
                i = _numberObjs
            End If
        Next i

        If Not FoundObj Then
            LogASLError("No such object '" & ObjName & "' in condition 'action <" & ActionData & ">' ...", LogType.WarningError)
            Return False
        End If

        Result = False

        Dim o = _objs(ObjID)

        For i = 1 To o.NumberActions
            If LCase(o.Actions(i).ActionName) = LCase(ActionName) Then
                i = o.NumberActions
                Result = True
            End If
        Next i

        Return Result

    End Function

    Private Function ExecuteIfType(TypeData As String) As Boolean
        Dim SCP, ObjID As Integer
        Dim ObjName As String
        Dim TypeName As String
        Dim FoundObj As Boolean
        Dim Result As Boolean
        Dim i As Integer

        SCP = InStr(TypeData, ";")

        If SCP = 0 Then
            LogASLError("No type name given in condition 'type <" & TypeData & ">' ...", LogType.WarningError)
            Return False
        End If

        ObjName = Trim(Left(TypeData, SCP - 1))
        TypeName = Trim(Mid(TypeData, SCP + 1))

        FoundObj = False

        For i = 1 To _numberObjs
            If LCase(_objs(i).ObjectName) = LCase(ObjName) Then
                FoundObj = True
                ObjID = i
                i = _numberObjs
            End If
        Next i

        If Not FoundObj Then
            LogASLError("No such object '" & ObjName & "' in condition 'type <" & TypeData & ">' ...", LogType.WarningError)
            Return False
        End If

        Result = False

        Dim o = _objs(ObjID)

        For i = 1 To o.NumberTypesIncluded
            If LCase(o.TypesIncluded(i)) = LCase(TypeName) Then
                i = o.NumberTypesIncluded
                Result = True
            End If
        Next i

        Return Result

    End Function

    Private Function GetArrayIndex(sVarName As String, ctx As Context) As Integer
        Dim BeginPos, EndPos As Integer
        Dim ArrayIndexData As String
        Dim ArrayIndex As Integer

        If InStr(sVarName, "[") <> 0 And InStr(sVarName, "]") <> 0 Then
            BeginPos = InStr(sVarName, "[")
            EndPos = InStr(sVarName, "]")
            ArrayIndexData = Mid(sVarName, BeginPos + 1, (EndPos - BeginPos) - 1)
            If IsNumeric(ArrayIndexData) Then
                ArrayIndex = CInt(ArrayIndexData)
            Else
                ArrayIndex = CInt(GetNumericContents(ArrayIndexData, ctx))
            End If
            sVarName = Left(sVarName, BeginPos - 1)
        End If

        Return ArrayIndex

    End Function

    Friend Function Disambiguate(ObjectName As String, ContainedIn As String, ctx As Context, Optional bExit As Boolean = False) As Integer
        ' Returns object ID being referred to by player.
        ' Returns -1 if object doesn't exist, calling function
        '   then expected to print relevant error.
        ' Returns -2 if "it" meaningless, prints own error.
        ' If it returns an object ID, it also sets quest.lastobject to the name
        ' of the object referred to.
        ' If ctx.AllowRealNamesInCommand is True, will allow an object's real
        ' name to be used even when the object has an alias - this is used when
        ' Disambiguate has been called after an "exec" command to prevent the
        ' player having to choose an object from the disambiguation menu twice

        Dim OrigBeginsWithThe As Boolean
        OrigBeginsWithThe = False
        Dim NumberCorresIDs As Integer
        NumberCorresIDs = 0
        Dim IDNumbers(0) As Integer
        Dim FirstPlace As String
        Dim SecondPlace As String = ""
        Dim TwoPlaces As Boolean
        Dim DescriptionText() As String
        Dim ValidNames() As String
        Dim NumValidNames As Integer
        Dim i, j As Integer
        Dim ThisName As String

        ObjectName = Trim(ObjectName)

        SetStringContents("quest.lastobject", "", ctx)

        Dim SCP As Integer
        If InStr(ContainedIn, ";") <> 0 Then
            SCP = InStr(ContainedIn, ";")
            TwoPlaces = True
            FirstPlace = Trim(Left(ContainedIn, SCP - 1))
            SecondPlace = Trim(Mid(ContainedIn, SCP + 1))
        Else
            TwoPlaces = False
            FirstPlace = ContainedIn
        End If

        If ctx.AllowRealNamesInCommand Then
            For i = 1 To _numberObjs
                If DisambObjHere(ctx, i, FirstPlace, TwoPlaces, SecondPlace) Then
                    If LCase(_objs(i).ObjectName) = LCase(ObjectName) Then
                        SetStringContents("quest.lastobject", _objs(i).ObjectName, ctx)
                        Return i
                    End If
                End If
            Next i
        End If

        ' If player uses "it", "them" etc. as name:
        If ObjectName = "it" Or ObjectName = "them" Or ObjectName = "this" Or ObjectName = "those" Or ObjectName = "these" Or ObjectName = "that" Then
            SetStringContents("quest.error.pronoun", ObjectName, ctx)
            If _lastIt <> 0 And _lastItMode = ItType.Inanimate And DisambObjHere(ctx, _lastIt, FirstPlace, TwoPlaces, SecondPlace) Then
                SetStringContents("quest.lastobject", _objs(_lastIt).ObjectName, ctx)
                Return _lastIt
            Else
                PlayerErrorMessage(PlayerError.BadPronoun, ctx)
                Return -2
            End If
        ElseIf ObjectName = "him" Then
            SetStringContents("quest.error.pronoun", ObjectName, ctx)
            If _lastIt <> 0 And _lastItMode = ItType.Male And DisambObjHere(ctx, _lastIt, FirstPlace, TwoPlaces, SecondPlace) Then
                SetStringContents("quest.lastobject", _objs(_lastIt).ObjectName, ctx)
                Return _lastIt
            Else
                PlayerErrorMessage(PlayerError.BadPronoun, ctx)
                Return -2
            End If
        ElseIf ObjectName = "her" Then
            SetStringContents("quest.error.pronoun", ObjectName, ctx)
            If _lastIt <> 0 And _lastItMode = ItType.Female And DisambObjHere(ctx, _lastIt, FirstPlace, TwoPlaces, SecondPlace) Then
                SetStringContents("quest.lastobject", _objs(_lastIt).ObjectName, ctx)
                Return _lastIt
            Else
                PlayerErrorMessage(PlayerError.BadPronoun, ctx)
                Return -2
            End If
        End If

        _thisTurnIt = 0

        If BeginsWith(ObjectName, "the ") Then
            ObjectName = GetEverythingAfter(ObjectName, "the ")
        End If

        For i = 1 To _numberObjs

            If DisambObjHere(ctx, i, FirstPlace, TwoPlaces, SecondPlace, bExit) Then

                NumValidNames = _objs(i).NumberAltNames + 1
                ReDim ValidNames(NumValidNames)
                ValidNames(1) = _objs(i).ObjectAlias
                For j = 1 To _objs(i).NumberAltNames
                    ValidNames(j + 1) = _objs(i).AltNames(j)
                Next j

                For j = 1 To NumValidNames
                    If ((LCase(ValidNames(j)) = LCase(ObjectName)) Or ("the " & LCase(ObjectName) = LCase(ValidNames(j)))) Then
                        NumberCorresIDs = NumberCorresIDs + 1
                        ReDim Preserve IDNumbers(NumberCorresIDs)
                        IDNumbers(NumberCorresIDs) = i

                        j = NumValidNames
                    End If
                Next j
            End If
        Next i

        If _gameAslVersion >= 391 And NumberCorresIDs = 0 And _useAbbreviations And Len(ObjectName) > 0 Then
            ' Check for abbreviated object names

            For i = 1 To _numberObjs
                If DisambObjHere(ctx, i, FirstPlace, TwoPlaces, SecondPlace, bExit) Then
                    If _objs(i).ObjectAlias <> "" Then ThisName = LCase(_objs(i).ObjectAlias) Else ThisName = LCase(_objs(i).ObjectName)
                    If _gameAslVersion >= 410 Then
                        If _objs(i).Prefix <> "" Then ThisName = Trim(LCase(_objs(i).Prefix)) & " " & ThisName
                        If _objs(i).Suffix <> "" Then ThisName = ThisName & " " & Trim(LCase(_objs(i).Suffix))
                    End If
                    If InStr(" " & ThisName, " " & LCase(ObjectName)) <> 0 Then
                        NumberCorresIDs = NumberCorresIDs + 1
                        ReDim Preserve IDNumbers(NumberCorresIDs)
                        IDNumbers(NumberCorresIDs) = i
                    End If
                End If
            Next i
        End If

        Dim Question As String
        If NumberCorresIDs = 1 Then
            SetStringContents("quest.lastobject", _objs(IDNumbers(1)).ObjectName, ctx)
            _thisTurnIt = IDNumbers(1)

            Select Case _objs(IDNumbers(1)).Article
                Case "him"
                    _thisTurnItMode = ItType.Male
                Case "her"
                    _thisTurnItMode = ItType.Female
                Case Else
                    _thisTurnItMode = ItType.Inanimate
            End Select

            Return IDNumbers(1)
        ElseIf NumberCorresIDs > 1 Then
            ReDim DescriptionText(NumberCorresIDs)

            Question = "Please select which " & ObjectName & " you mean:"
            Print("- |i" & Question & "|xi", ctx)

            Dim menuItems As New Dictionary(Of String, String)

            For i = 1 To NumberCorresIDs
                DescriptionText(i) = _objs(IDNumbers(i)).Detail
                If DescriptionText(i) = "" Then
                    If _objs(IDNumbers(i)).Prefix = "" Then
                        DescriptionText(i) = _objs(IDNumbers(i)).ObjectAlias
                    Else
                        DescriptionText(i) = _objs(IDNumbers(i)).Prefix & _objs(IDNumbers(i)).ObjectAlias
                    End If
                End If

                menuItems.Add(CStr(i), DescriptionText(i))

            Next i

            Dim mnu As New MenuData(Question, menuItems, False)
            Dim response As String = ShowMenu(mnu)

            _choiceNumber = CInt(response)

            SetStringContents("quest.lastobject", _objs(IDNumbers(_choiceNumber)).ObjectName, ctx)

            _thisTurnIt = IDNumbers(_choiceNumber)

            Select Case _objs(IDNumbers(_choiceNumber)).Article
                Case "him"
                    _thisTurnItMode = ItType.Male
                Case "her"
                    _thisTurnItMode = ItType.Female
                Case Else
                    _thisTurnItMode = ItType.Inanimate
            End Select

            Print("- " & DescriptionText(_choiceNumber) & "|n", ctx)

            Return IDNumbers(_choiceNumber)
        End If

        _thisTurnIt = _lastIt
        SetStringContents("quest.error.object", ObjectName, ctx)
        Return -1
    End Function

    Private Function DisplayStatusVariableInfo(VarNum As Integer, VariableType As VarType, ctx As Context) As String
        Dim DisplayData As String = ""
        Dim ExcPos As Integer
        Dim FirstStar, SecondStar As Integer
        Dim BeforeStar, AfterStar As String
        Dim BetweenStar As String
        Dim ArrayIndex As Integer

        ArrayIndex = 0

        If VariableType = VarType.String Then
            DisplayData = ConvertVarsIn(_stringVariable(VarNum).DisplayString, ctx)
            ExcPos = InStr(DisplayData, "!")

            If ExcPos <> 0 Then
                DisplayData = Left(DisplayData, ExcPos - 1) & _stringVariable(VarNum).VariableContents(ArrayIndex) & Mid(DisplayData, ExcPos + 1)
            End If
        ElseIf VariableType = VarType.Numeric Then
            If _numericVariable(VarNum).NoZeroDisplay And Val(_numericVariable(VarNum).VariableContents(ArrayIndex)) = 0 Then
                Return ""
            End If
            DisplayData = ConvertVarsIn(_numericVariable(VarNum).DisplayString, ctx)
            ExcPos = InStr(DisplayData, "!")

            If ExcPos <> 0 Then
                DisplayData = Left(DisplayData, ExcPos - 1) & _numericVariable(VarNum).VariableContents(ArrayIndex) & Mid(DisplayData, ExcPos + 1)
            End If

            If InStr(DisplayData, "*") > 0 Then
                FirstStar = InStr(DisplayData, "*")
                SecondStar = InStr(FirstStar + 1, DisplayData, "*")
                BeforeStar = Left(DisplayData, FirstStar - 1)
                AfterStar = Mid(DisplayData, SecondStar + 1)
                BetweenStar = Mid(DisplayData, FirstStar + 1, (SecondStar - FirstStar) - 1)

                If CDbl(_numericVariable(VarNum).VariableContents(ArrayIndex)) <> 1 Then
                    DisplayData = BeforeStar & BetweenStar & AfterStar
                Else
                    DisplayData = BeforeStar & AfterStar
                End If
            End If
        End If

        Return DisplayData
    End Function

    Friend Function DoAction(ObjID As Integer, ActionName As String, ctx As Context, Optional LogError As Boolean = True) As Boolean

        Dim FoundAction As Boolean
        Dim ActionScript As String = ""
        Dim i As Integer

        Dim o = _objs(ObjID)

        For i = 1 To o.NumberActions
            If o.Actions(i).ActionName = LCase(ActionName) Then
                FoundAction = True
                ActionScript = o.Actions(i).Script
                Exit For
            End If
        Next i

        If Not FoundAction Then
            If LogError Then LogASLError("No such action '" & ActionName & "' defined for object '" & o.ObjectName & "'")
            Return False
        End If

        Dim NewThread As Context = CopyContext(ctx)
        NewThread.CallingObjectId = ObjID

        ExecuteScript(ActionScript, NewThread, ObjID)

        Return True
    End Function

    Public Function HasAction(ObjID As Integer, ActionName As String) As Boolean
        Dim i As Integer

        Dim o = _objs(ObjID)

        For i = 1 To o.NumberActions
            If o.Actions(i).ActionName = LCase(ActionName) Then
                Return True
            End If
        Next i

        Return False
    End Function

    Private Sub ExecForEach(ScriptLine As String, ctx As Context)
        Dim InLocation, ScriptToExecute As String
        Dim i, BracketPos As Integer
        Dim bExit As Boolean
        Dim bRoom As Boolean

        If BeginsWith(ScriptLine, "object ") Then
            ScriptLine = GetEverythingAfter(ScriptLine, "object ")
            If Not BeginsWith(ScriptLine, "in ") Then
                LogASLError("Expected 'in' in 'for each object " & ReportErrorLine(ScriptLine) & "'", LogType.WarningError)
                Exit Sub
            End If
        ElseIf BeginsWith(ScriptLine, "exit ") Then
            ScriptLine = GetEverythingAfter(ScriptLine, "exit ")
            If Not BeginsWith(ScriptLine, "in ") Then
                LogASLError("Expected 'in' in 'for each exit " & ReportErrorLine(ScriptLine) & "'", LogType.WarningError)
                Exit Sub
            End If
            bExit = True
        ElseIf BeginsWith(ScriptLine, "room ") Then
            ScriptLine = GetEverythingAfter(ScriptLine, "room ")
            If Not BeginsWith(ScriptLine, "in ") Then
                LogASLError("Expected 'in' in 'for each room " & ReportErrorLine(ScriptLine) & "'", LogType.WarningError)
                Exit Sub
            End If
            bRoom = True
        Else
            LogASLError("Unknown type in 'for each " & ReportErrorLine(ScriptLine) & "'", LogType.WarningError)
            Exit Sub
        End If

        ScriptLine = GetEverythingAfter(ScriptLine, "in ")

        If BeginsWith(ScriptLine, "game ") Then
            InLocation = ""
            ScriptToExecute = GetEverythingAfter(ScriptLine, "game ")
        Else
            InLocation = LCase(RetrieveParameter(ScriptLine, ctx))
            BracketPos = InStr(ScriptLine, ">")
            ScriptToExecute = Trim(Mid(ScriptLine, BracketPos + 1))
        End If

        For i = 1 To _numberObjs
            If InLocation = "" Or LCase(_objs(i).ContainerRoom) = InLocation Then
                If _objs(i).IsRoom = bRoom And _objs(i).IsExit = bExit Then
                    SetStringContents("quest.thing", _objs(i).ObjectName, ctx)
                    ExecuteScript(ScriptToExecute, ctx)
                End If
            End If
        Next i
    End Sub

    Private Sub ExecuteAction(ActionData As String, ctx As Context)
        Dim FoundExisting As Boolean
        Dim ActionName As String
        Dim ActionScript As String
        Dim EP, SCP As Integer
        Dim ActionNum As Integer
        Dim ActionParam As String
        Dim ObjName As String
        Dim FoundObject As Boolean
        Dim ObjID, i As Integer

        FoundExisting = False
        FoundObject = False

        ActionParam = RetrieveParameter(ActionData, ctx)
        SCP = InStr(ActionParam, ";")
        If SCP = 0 Then
            LogASLError("No action name specified in 'action " & ActionData & "'", LogType.WarningError)
            Exit Sub
        End If

        ObjName = Trim(Left(ActionParam, SCP - 1))
        ActionName = Trim(Mid(ActionParam, SCP + 1))

        EP = InStr(ActionData, ">")
        If EP = Len(Trim(ActionData)) Then
            ActionScript = ""
        Else
            ActionScript = Trim(Mid(ActionData, EP + 1))
        End If

        For i = 1 To _numberObjs
            If LCase(_objs(i).ObjectName) = LCase(ObjName) Then
                FoundObject = True
                ObjID = i
                i = _numberObjs
            End If
        Next i

        If Not FoundObject Then
            LogASLError("No such object '" & ObjName & "' in 'action " & ActionData & "'", LogType.WarningError)
            Exit Sub
        End If

        Dim o = _objs(ObjID)

        For i = 1 To o.NumberActions
            If o.Actions(i).ActionName = ActionName Then
                FoundExisting = True
                ActionNum = i
                i = o.NumberActions
            End If
        Next i

        If Not FoundExisting Then
            o.NumberActions = o.NumberActions + 1
            ReDim Preserve o.Actions(o.NumberActions)
            o.Actions(o.NumberActions) = New ActionType
            ActionNum = o.NumberActions
        End If

        o.Actions(ActionNum).ActionName = ActionName
        o.Actions(ActionNum).Script = ActionScript

        ObjectActionUpdate(ObjID, ActionName, ActionScript)

    End Sub

    Private Function ExecuteCondition(Condition As String, ctx As Context) As Boolean
        Dim bThisResult, bThisNot As Boolean

        If BeginsWith(Condition, "not ") Then
            bThisNot = True
            Condition = GetEverythingAfter(Condition, "not ")
        Else
            bThisNot = False
        End If

        If BeginsWith(Condition, "got ") Then
            bThisResult = ExecuteIfGot(RetrieveParameter(Condition, ctx))
        ElseIf BeginsWith(Condition, "has ") Then
            bThisResult = ExecuteIfHas(RetrieveParameter(Condition, ctx))
        ElseIf BeginsWith(Condition, "ask ") Then
            bThisResult = ExecuteIfAsk(RetrieveParameter(Condition, ctx))
        ElseIf BeginsWith(Condition, "is ") Then
            bThisResult = ExecuteIfIs(RetrieveParameter(Condition, ctx))
        ElseIf BeginsWith(Condition, "here ") Then
            bThisResult = ExecuteIfHere(RetrieveParameter(Condition, ctx), ctx)
        ElseIf BeginsWith(Condition, "exists ") Then
            bThisResult = ExecuteIfExists(RetrieveParameter(Condition, ctx), False)
        ElseIf BeginsWith(Condition, "real ") Then
            bThisResult = ExecuteIfExists(RetrieveParameter(Condition, ctx), True)
        ElseIf BeginsWith(Condition, "property ") Then
            bThisResult = ExecuteIfProperty(RetrieveParameter(Condition, ctx))
        ElseIf BeginsWith(Condition, "action ") Then
            bThisResult = ExecuteIfAction(RetrieveParameter(Condition, ctx))
        ElseIf BeginsWith(Condition, "type ") Then
            bThisResult = ExecuteIfType(RetrieveParameter(Condition, ctx))
        ElseIf BeginsWith(Condition, "flag ") Then
            bThisResult = ExecuteIfFlag(RetrieveParameter(Condition, ctx))
        End If

        If bThisNot Then bThisResult = Not bThisResult

        Return bThisResult
    End Function

    Private Function ExecuteConditions(ConditionList As String, ctx As Context) As Boolean
        Dim Conditions() As String
        Dim iNumConditions As Integer
        Dim Operations() As String
        Dim bThisResult As Boolean
        Dim bConditionResult As Boolean
        Dim iCurLinePos As Integer
        Dim bFinalCondition As Boolean
        Dim ObscuredConditionList, NextCondition As String
        Dim NextConditionPos As Integer
        Dim ThisCondition As String
        Dim i As Integer

        ObscuredConditionList = ObliterateParameters(ConditionList)

        iCurLinePos = 1 : bFinalCondition = False
        iNumConditions = 0

        Do
            iNumConditions = iNumConditions + 1
            ReDim Preserve Conditions(iNumConditions)
            ReDim Preserve Operations(iNumConditions)

            NextCondition = "AND"
            NextConditionPos = InStr(iCurLinePos, ObscuredConditionList, "and ")
            If NextConditionPos = 0 Then
                NextConditionPos = InStr(iCurLinePos, ObscuredConditionList, "or ")
                NextCondition = "OR"
            End If

            If NextConditionPos = 0 Then
                NextConditionPos = Len(ObscuredConditionList) + 2
                bFinalCondition = True
                NextCondition = "FINAL"
            End If

            ThisCondition = Trim(Mid(ConditionList, iCurLinePos, NextConditionPos - iCurLinePos - 1))

            Conditions(iNumConditions) = ThisCondition
            Operations(iNumConditions) = NextCondition

            ' next condition starts from space after and/or
            iCurLinePos = InStr(NextConditionPos, ObscuredConditionList, " ")
        Loop Until bFinalCondition

        Operations(0) = "AND"
        bConditionResult = True

        For i = 1 To iNumConditions
            bThisResult = ExecuteCondition(Conditions(i), ctx)

            If Operations(i - 1) = "AND" Then
                bConditionResult = bThisResult And bConditionResult
            ElseIf Operations(i - 1) = "OR" Then
                bConditionResult = bThisResult Or bConditionResult
            End If
        Next i

        Return bConditionResult

    End Function

    Private Sub ExecuteCreate(CreateData As String, ctx As Context)
        Dim NewName As String
        Dim SCP As Integer
        Dim ParamData As String
        Dim j As Integer

        Dim ContainerRoom As String
        If BeginsWith(CreateData, "room ") Then
            NewName = RetrieveParameter(CreateData, ctx)
            _numberRooms = _numberRooms + 1
            ReDim Preserve _rooms(_numberRooms)
            _rooms(_numberRooms) = New RoomType
            _rooms(_numberRooms).RoomName = NewName

            _numberObjs = _numberObjs + 1
            ReDim Preserve _objs(_numberObjs)
            _objs(_numberObjs) = New ObjectType
            _objs(_numberObjs).ObjectName = NewName
            _objs(_numberObjs).IsRoom = True
            _objs(_numberObjs).CorresRoom = NewName
            _objs(_numberObjs).CorresRoomId = _numberRooms

            _rooms(_numberRooms).ObjId = _numberObjs

            AddToChangeLog("room " & NewName, "create")

            If _gameAslVersion >= 410 Then
                AddToObjectProperties(_defaultRoomProperties.Properties, _numberObjs, ctx)
                For j = 1 To _defaultRoomProperties.NumberActions
                    AddObjectAction(_numberObjs, _defaultRoomProperties.Actions(j).ActionName, _defaultRoomProperties.Actions(j).Script)
                Next j

                _rooms(_numberRooms).Exits = New RoomExits(Me)
                _rooms(_numberRooms).Exits.ObjID = _rooms(_numberRooms).ObjId
            End If

        ElseIf BeginsWith(CreateData, "object ") Then
            ParamData = RetrieveParameter(CreateData, ctx)
            SCP = InStr(ParamData, ";")
            If SCP = 0 Then
                NewName = ParamData
                ContainerRoom = ""
            Else
                NewName = Trim(Left(ParamData, SCP - 1))
                ContainerRoom = Trim(Mid(ParamData, SCP + 1))
            End If

            _numberObjs = _numberObjs + 1
            ReDim Preserve _objs(_numberObjs)
            _objs(_numberObjs) = New ObjectType

            Dim o = _objs(_numberObjs)
            o.ObjectName = NewName
            o.ObjectAlias = NewName
            o.ContainerRoom = ContainerRoom
            o.Exists = True
            o.Visible = True
            o.Gender = "it"
            o.Article = "it"

            AddToChangeLog("object " & NewName, "create " & _objs(_numberObjs).ContainerRoom)

            If _gameAslVersion >= 410 Then
                AddToObjectProperties(_defaultProperties.Properties, _numberObjs, ctx)
                For j = 1 To _defaultProperties.NumberActions
                    AddObjectAction(_numberObjs, _defaultProperties.Actions(j).ActionName, _defaultProperties.Actions(j).Script)
                Next j
            End If

            If Not _gameLoading Then UpdateObjectList(ctx)

        ElseIf BeginsWith(CreateData, "exit ") Then
            ExecuteCreateExit(CreateData, ctx)
        End If
    End Sub

    Private Sub ExecuteCreateExit(CreateData As String, ctx As Context)
        Dim ExitData As String
        Dim SrcRoom As String
        Dim DestRoom As String = ""
        Dim SrcID, DestID As Integer
        Dim NewName As String
        Dim SCP As Integer
        Dim ExitExists As Boolean
        Dim i As Integer
        Dim ParamPos As Integer
        Dim SaveData As String

        ExitData = GetEverythingAfter(CreateData, "exit ")
        NewName = RetrieveParameter(CreateData, ctx)
        SCP = InStr(NewName, ";")
        If _gameAslVersion < 410 Then
            If SCP = 0 Then
                LogASLError("No exit destination given in 'create exit " & ExitData & "'", LogType.WarningError)
                Exit Sub
            End If
        End If

        If SCP = 0 Then
            SrcRoom = Trim(NewName)
        Else
            SrcRoom = Trim(Left(NewName, SCP - 1))
        End If
        SrcID = GetRoomID(SrcRoom, ctx)

        If SrcID = 0 Then
            LogASLError("No such room '" & SrcRoom & "'", LogType.WarningError)
            Exit Sub
        End If

        If _gameAslVersion < 410 Then
            ' only do destination room check for ASL <410, as can now have scripts on dynamically
            ' created exits, so the destination doesn't necessarily have to exist.

            DestRoom = Trim(Mid(NewName, SCP + 1))
            If DestRoom <> "" Then
                DestID = GetRoomID(DestRoom, ctx)

                If DestID = 0 Then
                    LogASLError("No such room '" & DestRoom & "'", LogType.WarningError)
                    Exit Sub
                End If
            End If
        End If

        ' If it's a "go to" exit, check if it already exists:
        ExitExists = False
        If BeginsWith(ExitData, "<") Then

            If _gameAslVersion >= 410 Then
                ExitExists = _rooms(SrcID).Exits.Places.ContainsKey(DestRoom)
            Else
                For i = 1 To _rooms(SrcID).NumberPlaces
                    If LCase(_rooms(SrcID).Places(i).PlaceName) = LCase(DestRoom) Then
                        ExitExists = True
                        i = _rooms(SrcID).NumberPlaces
                    End If
                Next i
            End If

            If ExitExists Then
                LogASLError("Exit from '" & SrcRoom & "' to '" & DestRoom & "' already exists", LogType.WarningError)
                Exit Sub
            End If
        End If

        ParamPos = InStr(ExitData, "<")
        If ParamPos = 0 Then
            SaveData = ExitData
        Else
            SaveData = Left(ExitData, ParamPos - 1)
            ' We do this so the changelog doesn't contain unconverted variable names
            SaveData = SaveData & "<" & RetrieveParameter(ExitData, ctx) & ">"
        End If
        AddToChangeLog("room " & _rooms(SrcID).RoomName, "exit " & SaveData)

        Dim r = _rooms(SrcID)

        If _gameAslVersion >= 410 Then
            r.Exits.AddExitFromCreateScript(ExitData, ctx)
        Else
            If BeginsWith(ExitData, "north ") Then
                r.North.Data = DestRoom
                r.North.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "south ") Then
                r.South.Data = DestRoom
                r.South.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "east ") Then
                r.East.Data = DestRoom
                r.East.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "west ") Then
                r.West.Data = DestRoom
                r.West.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "northeast ") Then
                r.NorthEast.Data = DestRoom
                r.NorthEast.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "northwest ") Then
                r.NorthWest.Data = DestRoom
                r.NorthWest.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "southeast ") Then
                r.SouthEast.Data = DestRoom
                r.SouthEast.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "southwest ") Then
                r.SouthWest.Data = DestRoom
                r.SouthWest.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "up ") Then
                r.Up.Data = DestRoom
                r.Up.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "down ") Then
                r.Down.Data = DestRoom
                r.Down.Type = TextActionType.Text
            ElseIf BeginsWith(ExitData, "out ") Then
                r.Out.Text = DestRoom
            ElseIf BeginsWith(ExitData, "<") Then
                r.NumberPlaces = r.NumberPlaces + 1
                ReDim Preserve r.Places(r.NumberPlaces)
                r.Places(r.NumberPlaces) = New PlaceType
                r.Places(r.NumberPlaces).PlaceName = DestRoom
            Else
                LogASLError("Invalid direction in 'create exit " & ExitData & "'", LogType.WarningError)
            End If
        End If

        If Not _gameLoading Then
            ' Update quest.doorways variables
            ShowRoomInfo(_currentRoom, ctx, True)

            UpdateObjectList(ctx)

            If _gameAslVersion < 410 Then
                If _currentRoom = _rooms(SrcID).RoomName Then
                    UpdateDoorways(SrcID, ctx)
                ElseIf _currentRoom = _rooms(DestID).RoomName Then
                    UpdateDoorways(DestID, ctx)
                End If
            Else
                ' Don't have DestID in ASL410 CreateExit code, so just UpdateDoorways
                ' for current room anyway.
                UpdateDoorways(GetRoomID(_currentRoom, ctx), ctx)
            End If
        End If
    End Sub

    Private Sub ExecDrop(DropItem As String, ctx As Context)
        Dim FoundItem, ObjectIsInContainer As Boolean
        Dim ParentID, ObjectID, i As Integer
        Dim Parent As String
        Dim ParentDisplayName As String

        ObjectID = Disambiguate(DropItem, "inventory", ctx)

        If ObjectID > 0 Then
            FoundItem = True
        Else
            FoundItem = False
        End If

        If Not FoundItem Then
            If ObjectID <> -2 Then
                If _gameAslVersion >= 391 Then
                    PlayerErrorMessage(PlayerError.NoItem, ctx)
                Else
                    PlayerErrorMessage(PlayerError.BadDrop, ctx)
                End If
            End If
            _badCmdBefore = "drop"
            Exit Sub
        End If

        ' If object is inside a container, it must be removed before it can be dropped.
        ObjectIsInContainer = False
        If _gameAslVersion >= 391 Then
            If IsYes(GetObjectProperty("parent", ObjectID, True, False)) Then
                ObjectIsInContainer = True
                Parent = GetObjectProperty("parent", ObjectID, False, False)
                ParentID = GetObjectIDNoAlias(Parent)
            End If
        End If

        Dim DropFound As Boolean
        Dim DropStatement As String = ""
        DropFound = False

        For i = _objs(ObjectID).DefinitionSectionStart To _objs(ObjectID).DefinitionSectionEnd
            If BeginsWith(_lines(i), "drop ") Then
                DropStatement = GetEverythingAfter(_lines(i), "drop ")
                DropFound = True
                i = _objs(ObjectID).DefinitionSectionEnd
            End If
        Next i

        SetStringContents("quest.error.article", _objs(ObjectID).Article, ctx)

        If Not DropFound Or BeginsWith(DropStatement, "everywhere") Then
            If ObjectIsInContainer Then
                ' So, we want to drop an object that's in a container or surface. So first
                ' we have to remove the object from that container.

                If _objs(ParentID).ObjectAlias <> "" Then
                    ParentDisplayName = _objs(ParentID).ObjectAlias
                Else
                    ParentDisplayName = _objs(ParentID).ObjectName
                End If

                Print("(first removing " & _objs(ObjectID).Article & " from " & ParentDisplayName & ")", ctx)

                ' Try to remove the object
                ctx.AllowRealNamesInCommand = True
                ExecCommand("remove " & _objs(ObjectID).ObjectName, ctx, False, , True)

                If GetObjectProperty("parent", ObjectID, False, False) <> "" Then
                    ' removing the object failed
                    Exit Sub
                End If
            End If
        End If

        If Not DropFound Then
            PlayerErrorMessage(PlayerError.DefaultDrop, ctx)
            PlayerItem(_objs(ObjectID).ObjectName, False, ctx)
        Else
            If BeginsWith(DropStatement, "everywhere") Then
                PlayerItem(_objs(ObjectID).ObjectName, False, ctx)
                If InStr(DropStatement, "<") <> 0 Then
                    Print(RetrieveParameter(InputString:=DropStatement, ctx:=ctx), ctx)
                Else
                    PlayerErrorMessage(PlayerError.DefaultDrop, ctx)
                End If
            ElseIf BeginsWith(DropStatement, "nowhere") Then
                If InStr(DropStatement, "<") <> 0 Then
                    Print(RetrieveParameter(InputString:=DropStatement, ctx:=ctx), ctx)
                Else
                    PlayerErrorMessage(PlayerError.CantDrop, ctx)
                End If
            Else
                ExecuteScript(DropStatement, ctx)
            End If
        End If

    End Sub

    Private Sub ExecExamine(CommandInfo As String, ctx As Context)
        Dim ExamineItem, ExamineAction As String
        Dim FoundItem As Boolean
        Dim FoundExamineAction As Boolean
        Dim i, ObjID, j As Integer
        Dim InventoryPlace As String

        FoundExamineAction = False
        FoundItem = False

        InventoryPlace = "inventory"

        ExamineItem = LCase(Trim(GetEverythingAfter(CommandInfo, "examine ")))

        If ExamineItem = "" Then
            PlayerErrorMessage(PlayerError.BadExamine, ctx)
            _badCmdBefore = "examine"
            Exit Sub
        End If

        ObjID = Disambiguate(ExamineItem, _currentRoom & ";" & InventoryPlace, ctx)
        If ObjID > 0 Then
            FoundItem = True
        Else
            FoundItem = False
        End If

        If FoundItem Then
            Dim o = _objs(ObjID)

            ' Find "examine" action:
            For i = 1 To o.NumberActions
                If o.Actions(i).ActionName = "examine" Then
                    ExecuteScript(o.Actions(i).Script, ctx, ObjID)
                    FoundExamineAction = True
                    i = o.NumberActions
                End If
            Next i

            ' Find "examine" property:
            If Not FoundExamineAction Then
                For i = 1 To o.NumberProperties
                    If o.Properties(i).PropertyName = "examine" Then
                        Print(o.Properties(i).PropertyValue, ctx)
                        FoundExamineAction = True
                        i = o.NumberProperties
                    End If
                Next i
            End If

            ' Find "examine" tag:
            If Not FoundExamineAction Then
                For j = o.DefinitionSectionStart + 1 To _objs(ObjID).DefinitionSectionEnd - 1
                    If BeginsWith(_lines(j), "examine ") Then
                        ExamineAction = Trim(GetEverythingAfter(_lines(j), "examine "))
                        If Left(ExamineAction, 1) = "<" Then
                            Print(RetrieveParameter(_lines(j), ctx), ctx)
                        Else
                            ExecuteScript(ExamineAction, ctx, ObjID)
                        End If
                        FoundExamineAction = True
                    End If
                Next j
            End If

            If Not FoundExamineAction Then
                DoLook(ObjID, ctx, True)
            End If

        End If

        If Not FoundItem Then
            If ObjID <> -2 Then PlayerErrorMessage(PlayerError.BadThing, ctx)
            _badCmdBefore = "examine"
        End If

    End Sub

    Private Sub ExecMoveThing(sThingData As String, sThingType As Thing, ctx As Context)
        Dim SemiColonPos As Integer
        Dim ThingName, MoveToPlace As String

        SemiColonPos = InStr(sThingData, ";")

        ThingName = Trim(Left(sThingData, SemiColonPos - 1))
        MoveToPlace = Trim(Mid(sThingData, SemiColonPos + 1))

        MoveThing(ThingName, MoveToPlace, sThingType, ctx)

    End Sub

    Private Sub ExecProperty(PropertyData As String, ctx As Context)
        Dim SCP As Integer
        Dim ObjName, Properties As String
        Dim ObjID, i As Integer
        Dim Found As Boolean
        SCP = InStr(PropertyData, ";")

        If SCP = 0 Then
            LogASLError("No property data given in 'property <" & PropertyData & ">'", LogType.WarningError)
            Exit Sub
        End If

        ObjName = Trim(Left(PropertyData, SCP - 1))
        Properties = Trim(Mid(PropertyData, SCP + 1))

        For i = 1 To _numberObjs
            If LCase(_objs(i).ObjectName) = LCase(ObjName) Then
                Found = True
                ObjID = i
                i = _numberObjs
            End If
        Next i

        If Not Found Then
            LogASLError("No such object in 'property <" & PropertyData & ">'", LogType.WarningError)
            Exit Sub
        End If

        AddToObjectProperties(Properties, ObjID, ctx)

    End Sub

    Private Sub ExecuteDo(ProcedureName As String, ctx As Context)
        Dim ProcedureBlock As DefineBlock
        Dim i, BracketPos As Integer
        Dim CloseBracketPos As Integer
        Dim ParameterData As String
        Dim NewThread As Context = CopyContext(ctx)
        Dim iNumParameters As Integer
        Dim RunInNewThread As Boolean
        Dim iCurPos, SCP As Integer

        If _gameAslVersion >= 392 And Left(ProcedureName, 8) = "!intproc" Then
            ' If "do" procedure is run in a new thread, thread info is not passed to any nested
            ' script blocks in braces.

            RunInNewThread = False
        Else
            RunInNewThread = True
        End If

        If _gameAslVersion >= 284 Then
            BracketPos = InStr(ProcedureName, "(")
            If BracketPos <> 0 Then
                CloseBracketPos = InStr(BracketPos + 1, ProcedureName, ")")
            End If

            If BracketPos <> 0 And CloseBracketPos <> 0 Then
                ParameterData = Mid(ProcedureName, BracketPos + 1, (CloseBracketPos - BracketPos) - 1)
                ProcedureName = Left(ProcedureName, BracketPos - 1)

                ParameterData = ParameterData & ";"
                iCurPos = 1
                Do
                    iNumParameters = iNumParameters + 1
                    SCP = InStr(iCurPos, ParameterData, ";")

                    NewThread.NumParameters = iNumParameters
                    ReDim Preserve NewThread.Parameters(iNumParameters)
                    NewThread.Parameters(iNumParameters) = Trim(Mid(ParameterData, iCurPos, SCP - iCurPos))

                    iCurPos = SCP + 1
                Loop Until iCurPos >= Len(ParameterData)
            End If
        End If

        ProcedureBlock = DefineBlockParam("procedure", ProcedureName)
        If ProcedureBlock.StartLine = 0 And ProcedureBlock.EndLine = 0 Then
            LogASLError("No such procedure " & ProcedureName, LogType.WarningError)
        Else
            For i = ProcedureBlock.StartLine + 1 To ProcedureBlock.EndLine - 1
                If Not RunInNewThread Then
                    ExecuteScript((_lines(i)), ctx)
                Else
                    ExecuteScript((_lines(i)), NewThread)
                    ctx.DontProcessCommand = NewThread.DontProcessCommand
                End If
            Next i
        End If
    End Sub

    Private Sub ExecuteDoAction(ActionData As String, ctx As Context)
        Dim SCP As Integer
        Dim ObjName, ActionName As String
        Dim ObjID, i As Integer
        Dim FoundID As Boolean

        SCP = InStr(ActionData, ";")
        If SCP = 0 Then
            LogASLError("No action name specified in 'doaction <" & ActionData & ">'")
            Exit Sub
        End If

        ObjName = LCase(Trim(Left(ActionData, SCP - 1)))
        ActionName = Trim(Mid(ActionData, SCP + 1))

        FoundID = False

        For i = 1 To _numberObjs
            If LCase(_objs(i).ObjectName) = ObjName Then
                FoundID = True
                ObjID = i
                i = _numberObjs
            End If
        Next i

        If Not FoundID Then
            LogASLError("No such object '" & ObjName & "'")
            Exit Sub
        End If

        DoAction(ObjID, ActionName, ctx)

    End Sub

    Private Function ExecuteIfHere(HereThing As String, ctx As Context) As Boolean
        Dim bResult, bFound As Boolean
        Dim i As Integer

        bFound = False
        bResult = False

        If _gameAslVersion <= 281 Then
            For i = 1 To _numberChars
                If _chars(i).ContainerRoom = _currentRoom And _chars(i).Exists Then
                    If LCase(HereThing) = LCase(_chars(i).ObjectName) Then
                        bResult = True
                        bFound = True
                        i = _numberChars
                    End If
                End If
            Next i
        End If

        If Not bFound Then
            For i = 1 To _numberObjs
                If LCase(_objs(i).ContainerRoom) = LCase(_currentRoom) And _objs(i).Exists Then
                    If LCase(HereThing) = LCase(_objs(i).ObjectName) Then
                        bResult = True
                        bFound = True
                        i = _numberObjs
                    End If
                End If
            Next i
        End If

        If bFound = False Then
            bResult = False
        End If

        Return bResult
    End Function

    Private Function ExecuteIfExists(ExistsThing As String, RealCheckOnly As Boolean) As Boolean
        Dim bResult, bFound As Boolean
        Dim bErrorReport As Boolean
        Dim i As Integer
        bErrorReport = False

        Dim SCP As Integer
        If InStr(ExistsThing, ";") <> 0 Then
            SCP = InStr(ExistsThing, ";")
            If LCase(Trim(Mid(ExistsThing, SCP + 1))) = "report" Then
                bErrorReport = True
            End If

            ExistsThing = Left(ExistsThing, SCP - 1)
        End If

        bFound = False

        If _gameAslVersion < 281 Then
            For i = 1 To _numberChars
                If LCase(ExistsThing) = LCase(_chars(i).ObjectName) Then
                    If _chars(i).Exists Then
                        bResult = True
                    Else
                        bResult = False
                    End If

                    bFound = True
                    i = _numberChars
                End If
            Next i
        End If

        If Not bFound Then
            For i = 1 To _numberObjs
                If LCase(ExistsThing) = LCase(_objs(i).ObjectName) Then
                    If _objs(i).Exists Then
                        bResult = True
                    Else
                        bResult = False
                    End If

                    bFound = True
                    i = _numberObjs
                End If
            Next i
        End If

        If bFound = False And bErrorReport Then
            LogASLError("No such character/object '" & ExistsThing & "'.", LogType.UserError)
        End If

        If bFound = False Then bResult = False

        If RealCheckOnly Then
            Return bFound
        End If

        Return bResult
    End Function

    Private Function ExecuteIfProperty(PropertyData As String) As Boolean
        Dim SCP, ObjID As Integer
        Dim ObjName As String
        Dim PropertyName As String
        Dim FoundObj As Boolean
        Dim i As Integer

        SCP = InStr(PropertyData, ";")

        If SCP = 0 Then
            LogASLError("No property name given in condition 'property <" & PropertyData & ">' ...", LogType.WarningError)
            Return False
        End If

        ObjName = Trim(Left(PropertyData, SCP - 1))
        PropertyName = Trim(Mid(PropertyData, SCP + 1))

        FoundObj = False

        For i = 1 To _numberObjs
            If LCase(_objs(i).ObjectName) = LCase(ObjName) Then
                FoundObj = True
                ObjID = i
                i = _numberObjs
            End If
        Next i

        If Not FoundObj Then
            LogASLError("No such object '" & ObjName & "' in condition 'property <" & PropertyData & ">' ...", LogType.WarningError)
            Return False
        End If

        Return GetObjectProperty(PropertyName, ObjID, True) = "yes"
    End Function

    Private Sub ExecuteRepeat(RepeatData As String, ctx As Context)
        Dim RepeatWhileTrue As Boolean
        Dim RepeatScript As String = ""
        Dim BracketPos As Integer
        Dim Conditions As String
        Dim FinishedLoop As Boolean
        Dim CurPos As Integer
        Dim AfterBracket As String
        Dim FoundScript As Boolean
        FoundScript = False

        If BeginsWith(RepeatData, "while ") Then
            RepeatWhileTrue = True
            RepeatData = GetEverythingAfter(RepeatData, "while ")
        ElseIf BeginsWith(RepeatData, "until ") Then
            RepeatWhileTrue = False
            RepeatData = GetEverythingAfter(RepeatData, "until ")
        Else
            LogASLError("Expected 'until' or 'while' in 'repeat " & ReportErrorLine(RepeatData) & "'", LogType.WarningError)
            Exit Sub
        End If

        CurPos = 1
        Do
            BracketPos = InStr(CurPos, RepeatData, ">")
            AfterBracket = Trim(Mid(RepeatData, BracketPos + 1))
            If (Not BeginsWith(AfterBracket, "and ")) And (Not BeginsWith(AfterBracket, "or ")) Then
                RepeatScript = AfterBracket
                FoundScript = True
            Else
                CurPos = BracketPos + 1
            End If
        Loop Until FoundScript Or AfterBracket = ""

        Conditions = Trim(Left(RepeatData, BracketPos))

        FinishedLoop = False

        Do
            If ExecuteConditions(Conditions, ctx) = RepeatWhileTrue Then
                ExecuteScript(RepeatScript, ctx)
            Else
                FinishedLoop = True
            End If
        Loop Until FinishedLoop Or m_gameFinished
    End Sub

    Private Sub ExecuteSetCollectable(setparam As String, ctx As Context)
        Dim NewVal As Double
        Dim SemiColonPos As Integer
        Dim FoundCollectable As Boolean
        Dim CName, TheNewVal As String
        Dim i As Integer
        Dim ColNum As Integer
        Dim OP, TheNewValue As String

        SemiColonPos = InStr(setparam, ";")
        CName = Trim(Left(setparam, SemiColonPos - 1))
        TheNewVal = Trim(Right(setparam, Len(setparam) - SemiColonPos))

        FoundCollectable = False

        For i = 1 To _numCollectables
            If _collectables(i).Name = CName Then
                ColNum = i
                i = _numCollectables
                FoundCollectable = True
            End If
        Next i

        If Not FoundCollectable Then
            LogASLError("No such collectable '" & setparam & "'", LogType.WarningError)
            Exit Sub
        End If

        OP = Left(TheNewVal, 1)
        TheNewValue = Trim(Right(TheNewVal, Len(TheNewVal) - 1))
        If IsNumeric(TheNewValue) Then
            NewVal = Val(TheNewValue)
        Else
            NewVal = GetCollectableAmount(TheNewValue)
        End If

        If OP = "+" Then
            _collectables(ColNum).Value = _collectables(ColNum).Value + NewVal
        ElseIf OP = "-" Then
            _collectables(ColNum).Value = _collectables(ColNum).Value - NewVal
        ElseIf OP = "=" Then
            _collectables(ColNum).Value = NewVal
        End If

        CheckCollectable(ColNum)

        UpdateItems(ctx)
    End Sub

    Private Sub ExecuteWait(WaitLine As String, ctx As Context)
        If WaitLine <> "" Then
            Print(RetrieveParameter(WaitLine, ctx), ctx)
        Else

            If _gameAslVersion >= 410 Then
                PlayerErrorMessage(PlayerError.DefaultWait, ctx)
            Else
                Print("|nPress a key to continue...", ctx)
            End If
        End If

        DoWait()
    End Sub

    Private m_sFileData As String
    Private m_lIndex As Integer

    Private Sub InitFileData(FileData As String)
        m_sFileData = FileData
        m_lIndex = 1
    End Sub

    Private Function GetNextChunk() As String
        Dim NullPos As Integer
        NullPos = InStr(m_lIndex, m_sFileData, Chr(0))

        Dim result = Mid(m_sFileData, m_lIndex, NullPos - m_lIndex)

        If NullPos < Len(m_sFileData) Then
            m_lIndex = NullPos + 1
        End If

        Return result
    End Function

    Function GetFileDataChars(count As Integer) As String
        Dim result = Mid(m_sFileData, m_lIndex, count)
        m_lIndex = m_lIndex + count
        Return result
    End Function

    Private Function GetObjectActions(ActionInfo As String) As ActionType
        Dim ActionName As String
        Dim ActionScript As String
        Dim EP As Integer

        ActionName = LCase(RetrieveParameter(ActionInfo, _nullContext))
        EP = InStr(ActionInfo, ">")
        If EP = Len(ActionInfo) Then
            LogASLError("No script given for '" & ActionName & "' action data", LogType.WarningError)
            Return New ActionType
        End If

        ActionScript = Trim(Mid(ActionInfo, EP + 1))

        Dim result As ActionType = New ActionType
        result.ActionName = ActionName
        result.Script = ActionScript
        Return result
    End Function

    Private Function GetObjectID(ObjectName As String, ctx As Context, Optional ObjectRoom As String = "") As Integer
        Dim CurID, i As Integer
        Dim FoundItem As Boolean
        FoundItem = False

        If BeginsWith(ObjectName, "the ") Then
            ObjectName = GetEverythingAfter(ObjectName, "the ")
        End If

        For i = 1 To _numberObjs
            If (LCase(_objs(i).ObjectName) = LCase(ObjectName) Or LCase(_objs(i).ObjectName) = "the " & LCase(ObjectName)) And (LCase(_objs(i).ContainerRoom) = LCase(ObjectRoom) Or ObjectRoom = "") And _objs(i).Exists = True Then
                CurID = i
                FoundItem = True
                i = _numberObjs
            End If
        Next i

        If Not FoundItem And _gameAslVersion >= 280 Then
            CurID = Disambiguate(ObjectName, ObjectRoom, ctx)
            If CurID > 0 Then FoundItem = True
        End If

        If FoundItem Then
            Return CurID
        End If

        Return -1
    End Function

    Private Function GetObjectIDNoAlias(ObjectName As String) As Integer
        Dim i, ID As Integer
        Dim Found As Boolean

        Found = False

        For i = 1 To _numberObjs
            If LCase(_objs(i).ObjectName) = LCase(ObjectName) Then
                ID = i
                Found = True
                i = _numberObjs
            End If
        Next i

        If Not Found Then
            ID = 0
        End If

        Return ID
    End Function

    Friend Function GetObjectProperty(PropertyName As String, ObjID As Integer, Optional ReturnExistsOnly As Boolean = False, Optional LogError As Boolean = True) As String
        Dim bFound As Boolean
        Dim sResult As String = ""
        Dim i As Integer
        bFound = False

        Dim o = _objs(ObjID)

        For i = 1 To o.NumberProperties
            If LCase(o.Properties(i).PropertyName) = LCase(PropertyName) Then
                bFound = True
                sResult = o.Properties(i).PropertyValue
                i = o.NumberProperties
            End If
        Next i

        If ReturnExistsOnly Then
            If bFound Then
                Return "yes"
            End If
            Return "no"
        End If

        If bFound Then
            Return sResult
        End If

        If LogError Then
            LogASLError("Object '" & _objs(ObjID).ObjectName & "' has no property '" & PropertyName & "'", LogType.WarningError)
            Return "!"
        End If

        Return ""
    End Function

    Private Function GetPropertiesInType(TypeName As String, Optional bError As Boolean = True) As PropertiesActions
        Dim Found As Boolean
        Dim SecID As Integer
        Dim PropertyList As New PropertiesActions
        Dim NewProperties As PropertiesActions
        Dim IncTypeName As String
        Dim i, j As Integer
        Found = False

        For i = 1 To _numberSections
            If BeginsWith(_lines(_defineBlocks(i).StartLine), "define type") Then
                If LCase(RetrieveParameter(_lines(_defineBlocks(i).StartLine), _nullContext)) = LCase(TypeName) Then
                    SecID = i
                    i = _numberSections
                    Found = True
                End If
            End If
        Next i

        If Not Found Then
            If bError Then
                LogASLError("No such type '" & TypeName & "'", LogType.WarningError)
            End If
            Return New PropertiesActions
        End If

        For i = _defineBlocks(SecID).StartLine + 1 To _defineBlocks(SecID).EndLine - 1
            If BeginsWith(_lines(i), "type ") Then
                IncTypeName = LCase(RetrieveParameter(_lines(i), _nullContext))
                NewProperties = GetPropertiesInType(IncTypeName)
                PropertyList.Properties = PropertyList.Properties & NewProperties.Properties
                ReDim Preserve PropertyList.Actions(PropertyList.NumberActions + NewProperties.NumberActions)
                For j = PropertyList.NumberActions + 1 To PropertyList.NumberActions + NewProperties.NumberActions
                    PropertyList.Actions(j) = New ActionType
                    PropertyList.Actions(j).ActionName = NewProperties.Actions(j - PropertyList.NumberActions).ActionName
                    PropertyList.Actions(j).Script = NewProperties.Actions(j - PropertyList.NumberActions).Script
                Next j
                PropertyList.NumberActions = PropertyList.NumberActions + NewProperties.NumberActions

                ' Add this type name to the TypesIncluded list...
                PropertyList.NumberTypesIncluded = PropertyList.NumberTypesIncluded + 1
                ReDim Preserve PropertyList.TypesIncluded(PropertyList.NumberTypesIncluded)
                PropertyList.TypesIncluded(PropertyList.NumberTypesIncluded) = IncTypeName

                ' and add the names of the types included by it...

                ReDim Preserve PropertyList.TypesIncluded(PropertyList.NumberTypesIncluded + NewProperties.NumberTypesIncluded)
                For j = PropertyList.NumberTypesIncluded + 1 To PropertyList.NumberTypesIncluded + NewProperties.NumberTypesIncluded
                    PropertyList.TypesIncluded(j) = NewProperties.TypesIncluded(j - PropertyList.NumberTypesIncluded)
                Next j
                PropertyList.NumberTypesIncluded = PropertyList.NumberTypesIncluded + NewProperties.NumberTypesIncluded
            ElseIf BeginsWith(_lines(i), "action ") Then
                PropertyList.NumberActions = PropertyList.NumberActions + 1
                ReDim Preserve PropertyList.Actions(PropertyList.NumberActions)
                PropertyList.Actions(PropertyList.NumberActions) = GetObjectActions(GetEverythingAfter(_lines(i), "action "))
            ElseIf BeginsWith(_lines(i), "properties ") Then
                PropertyList.Properties = PropertyList.Properties & RetrieveParameter(_lines(i), _nullContext) & ";"
            ElseIf Trim(_lines(i)) <> "" Then
                PropertyList.Properties = PropertyList.Properties & _lines(i) & ";"
            End If
        Next i

        Return PropertyList
    End Function

    Friend Function GetRoomID(RoomName As String, ctx As Context) As Integer
        Dim ArrayIndex, i As Integer

        If InStr(RoomName, "[") > 0 Then
            ArrayIndex = GetArrayIndex(RoomName, ctx)
            RoomName = RoomName & Trim(Str(ArrayIndex))
        End If

        For i = 1 To _numberRooms
            If LCase(_rooms(i).RoomName) = LCase(RoomName) Then
                Return i
            End If
        Next i

        Return 0
    End Function

    Private Function GetTextOrScript(TextScript As String) As TextAction
        Dim result = New TextAction
        TextScript = Trim(TextScript)

        If Left(TextScript, 1) = "<" Then
            result.Type = TextActionType.Text
            result.Data = RetrieveParameter(TextScript, _nullContext)
        Else
            result.Type = TextActionType.Script
            result.Data = TextScript
        End If

        Return result
    End Function

    Private Function GetThingNumber(ThingName As String, ThingRoom As String, ThingType As Thing) As Integer
        ' Returns the number in the Chars() or _objs() array of the specified char/obj

        Dim i As Integer

        If ThingType = Thing.Character Then
            For i = 1 To _numberChars
                If (ThingRoom <> "" And LCase(_chars(i).ObjectName) = LCase(ThingName) And LCase(_chars(i).ContainerRoom) = LCase(ThingRoom)) Or (ThingRoom = "" And LCase(_chars(i).ObjectName) = LCase(ThingName)) Then
                    Return i
                End If
            Next i
        ElseIf ThingType = Thing.Object Then
            For i = 1 To _numberObjs
                If (ThingRoom <> "" And LCase(_objs(i).ObjectName) = LCase(ThingName) And LCase(_objs(i).ContainerRoom) = LCase(ThingRoom)) Or (ThingRoom = "" And LCase(_objs(i).ObjectName) = LCase(ThingName)) Then
                    Return i
                End If
            Next i
        End If

        Return -1
    End Function

    Private Function GetThingBlock(ThingName As String, ThingRoom As String, ThingType As Thing) As DefineBlock
        ' Returns position where specified char/obj is defined in ASL code

        Dim i As Integer
        Dim result = New DefineBlock

        If ThingType = Thing.Character Then
            For i = 1 To _numberChars
                If LCase(_chars(i).ObjectName) = LCase(ThingName) And LCase(_chars(i).ContainerRoom) = LCase(ThingRoom) Then
                    result.StartLine = _chars(i).DefinitionSectionStart
                    result.EndLine = _chars(i).DefinitionSectionEnd
                    Return result
                End If
            Next i
        ElseIf ThingType = Thing.Object Then
            For i = 1 To _numberObjs
                If LCase(_objs(i).ObjectName) = LCase(ThingName) And LCase(_objs(i).ContainerRoom) = LCase(ThingRoom) Then
                    result.StartLine = _objs(i).DefinitionSectionStart
                    result.EndLine = _objs(i).DefinitionSectionEnd
                    Return result
                End If
            Next i
        End If

        result.StartLine = 0
        result.EndLine = 0
        Return result
    End Function

    Private Function MakeRestoreData() As String
        Dim FileData As New System.Text.StringBuilder
        Dim ObjectData(0) As ChangeType
        Dim RoomData(0) As ChangeType
        Dim NumObjectData As Integer
        Dim NumRoomData As Integer
        Dim i As Integer
        Dim j As Integer
        Dim ObjExists As String
        Dim ObjVisible As String
        Dim sAppliesTo As String
        Dim sChangeData As String
        Dim StartPoint As Integer

        ' <<< FILE HEADER DATA >>>

        FileData.Append("QUEST300" & Chr(0) & GetOriginalFilenameForQSG() & Chr(0))

        ' The start point for encrypted data is after the filename
        StartPoint = FileData.Length + 1

        FileData.Append(_currentRoom & Chr(0))

        ' Organise Change Log

        For i = 1 To _gameChangeData.NumberChanges
            If BeginsWith(_gameChangeData.ChangeData(i).AppliesTo, "object ") Then
                NumObjectData = NumObjectData + 1
                ReDim Preserve ObjectData(NumObjectData)
                ObjectData(NumObjectData) = New ChangeType
                ObjectData(NumObjectData) = _gameChangeData.ChangeData(i)
            ElseIf BeginsWith(_gameChangeData.ChangeData(i).AppliesTo, "room ") Then
                NumRoomData = NumRoomData + 1
                ReDim Preserve RoomData(NumRoomData)
                RoomData(NumRoomData) = New ChangeType
                RoomData(NumRoomData) = _gameChangeData.ChangeData(i)
            End If
        Next i

        ' <<< OBJECT CREATE/CHANGE DATA >>>

        FileData.Append(Trim(Str(NumObjectData + _changeLogObjects.Changes.Count)) & Chr(0))

        For i = 1 To NumObjectData
            FileData.Append(GetEverythingAfter(ObjectData(i).AppliesTo, "object ") & Chr(0) & ObjectData(i).Change & Chr(0))
        Next i

        For Each key As String In _changeLogObjects.Changes.Keys
            sAppliesTo = Split(key, "#")(0)
            sChangeData = _changeLogObjects.Changes.Item(key)

            FileData.Append(sAppliesTo & Chr(0) & sChangeData & Chr(0))
        Next

        ' <<< OBJECT EXIST/VISIBLE/ROOM DATA >>>

        FileData.Append(Trim(Str(_numberObjs)) & Chr(0))

        For i = 1 To _numberObjs
            If _objs(i).Exists Then
                ObjExists = Chr(1)
            Else
                ObjExists = Chr(0)
            End If

            If _objs(i).Visible Then
                ObjVisible = Chr(1)
            Else
                ObjVisible = Chr(0)
            End If

            FileData.Append(_objs(i).ObjectName & Chr(0) & ObjExists & ObjVisible & _objs(i).ContainerRoom & Chr(0))
        Next i

        ' <<< ROOM CREATE/CHANGE DATA >>>

        FileData.Append(Trim(Str(NumRoomData)) & Chr(0))

        For i = 1 To NumRoomData
            FileData.Append(GetEverythingAfter(RoomData(i).AppliesTo, "room ") & Chr(0) & RoomData(i).Change & Chr(0))
        Next i

        ' <<< TIMER STATE DATA >>>

        FileData.Append(Trim(Str(_numberTimers)) & Chr(0))

        For i = 1 To _numberTimers
            Dim t = _timers(i)
            FileData.Append(t.TimerName & Chr(0))

            If t.TimerActive Then
                FileData.Append(Chr(1))
            Else
                FileData.Append(Chr(0))
            End If

            FileData.Append(Trim(Str(t.TimerInterval)) & Chr(0))
            FileData.Append(Trim(Str(t.TimerTicks)) & Chr(0))
        Next i

        ' <<< STRING VARIABLE DATA >>>

        FileData.Append(Trim(Str(_numberStringVariables)) & Chr(0))

        For i = 1 To _numberStringVariables
            Dim s = _stringVariable(i)
            FileData.Append(s.VariableName & Chr(0) & Trim(Str(s.VariableUBound)) & Chr(0))

            For j = 0 To s.VariableUBound
                FileData.Append(s.VariableContents(j) & Chr(0))
            Next j
        Next i

        ' <<< NUMERIC VARIABLE DATA >>>

        FileData.Append(Trim(Str(_numberNumericVariables)) & Chr(0))

        For i = 1 To _numberNumericVariables
            Dim n = _numericVariable(i)
            FileData.Append(n.VariableName & Chr(0) & Trim(Str(n.VariableUBound)) & Chr(0))

            For j = 0 To n.VariableUBound
                FileData.Append(n.VariableContents(j) & Chr(0))
            Next j
        Next i

        ' Now encrypt data
        Dim sFileData As String
        Dim NewFileData As New System.Text.StringBuilder

        sFileData = FileData.ToString()

        NewFileData.Append(Left(sFileData, StartPoint - 1))

        For i = StartPoint To Len(sFileData)
            NewFileData.Append(Chr(255 - Asc(Mid(sFileData, i, 1))))
        Next i

        Return NewFileData.ToString()
    End Function

    Private Sub MoveThing(sThingName As String, sThingRoom As String, iThingType As Thing, ctx As Context)
        Dim i, iThingNum, ArrayIndex As Integer
        Dim OldRoom As String = ""

        iThingNum = GetThingNumber(sThingName, "", iThingType)

        If InStr(sThingRoom, "[") > 0 Then
            ArrayIndex = GetArrayIndex(sThingRoom, ctx)
            sThingRoom = sThingRoom & Trim(Str(ArrayIndex))
        End If

        If iThingType = Thing.Character Then
            _chars(iThingNum).ContainerRoom = sThingRoom
        ElseIf iThingType = Thing.Object Then
            OldRoom = _objs(iThingNum).ContainerRoom
            _objs(iThingNum).ContainerRoom = sThingRoom
        End If

        If _gameAslVersion >= 391 Then
            ' If this object contains other objects, move them too
            For i = 1 To _numberObjs
                If LCase(GetObjectProperty("parent", i, False, False)) = LCase(sThingName) Then
                    MoveThing(_objs(i).ObjectName, sThingRoom, iThingType, ctx)
                End If
            Next i
        End If

        UpdateObjectList(ctx)

        If BeginsWith(LCase(sThingRoom), "inventory") Or BeginsWith(LCase(OldRoom), "inventory") Then
            UpdateItems(ctx)
        End If

    End Sub

    Public Sub Pause(Duration As Integer)
        m_player.DoPause(Duration)
        ChangeState(State.Waiting)

        SyncLock _waitLock
            System.Threading.Monitor.Wait(_waitLock)
        End SyncLock
    End Sub

    Private Function ConvertParameter(sParameter As String, sConvertChar As String, ConvertAction As ConvertType, ctx As Context) As String
        ' Returns a string with functions, string and
        ' numeric variables executed or converted as
        ' appropriate, read for display/etc.

        Dim iCurStringPos As Integer
        Dim iVarPos As Integer
        Dim NewParam As String = ""
        Dim bFinishLoop As Boolean
        Dim sCurStringBit As String
        Dim sVarName As String
        Dim iNextPos As Integer

        iCurStringPos = 1
        bFinishLoop = False
        Do
            iVarPos = InStr(iCurStringPos, sParameter, sConvertChar)
            If iVarPos = 0 Then
                iVarPos = Len(sParameter) + 1
                bFinishLoop = True
            End If

            sCurStringBit = Mid(sParameter, iCurStringPos, iVarPos - iCurStringPos)
            NewParam = NewParam & sCurStringBit

            If bFinishLoop = False Then
                iNextPos = InStr(iVarPos + 1, sParameter, sConvertChar)

                If iNextPos = 0 Then
                    LogASLError("Line parameter <" & sParameter & "> has missing " & sConvertChar, LogType.WarningError)
                    Return "<ERROR>"
                End If

                sVarName = Mid(sParameter, iVarPos + 1, (iNextPos - 1) - iVarPos)

                If sVarName = "" Then
                    NewParam = NewParam & sConvertChar
                Else

                    If ConvertAction = ConvertType.Strings Then
                        NewParam = NewParam & GetStringContents(sVarName, ctx)
                    ElseIf ConvertAction = ConvertType.Functions Then
                        sVarName = EvaluateInlineExpressions(sVarName)
                        NewParam = NewParam & DoFunction(sVarName, ctx)
                    ElseIf ConvertAction = ConvertType.Numeric Then
                        NewParam = NewParam & Trim(Str(GetNumericContents(sVarName, ctx)))
                    ElseIf ConvertAction = ConvertType.Collectables Then
                        NewParam = NewParam & Trim(Str(GetCollectableAmount(sVarName)))
                    End If
                End If

                iCurStringPos = iNextPos + 1
            End If
        Loop Until bFinishLoop

        Return NewParam
    End Function

    Private Function DoFunction(FunctionData As String, ctx As Context) As String
        Dim FunctionName, FunctionParameter As String
        Dim sIntFuncResult As String = ""
        Dim bIntFuncExecuted As Boolean
        Dim ParameterData As String
        Dim ParamPos, EndParamPos As Integer
        Dim SCP, i As Integer

        bIntFuncExecuted = False

        ParamPos = InStr(FunctionData, "(")
        If ParamPos <> 0 Then
            FunctionName = Trim(Left(FunctionData, ParamPos - 1))
            EndParamPos = InStrRev(FunctionData, ")")
            If EndParamPos = 0 Then
                LogASLError("Expected ) in $" & FunctionData & "$", LogType.WarningError)
                Return ""
            End If
            FunctionParameter = Mid(FunctionData, ParamPos + 1, (EndParamPos - ParamPos) - 1)
        Else
            FunctionName = FunctionData
            FunctionParameter = ""
        End If

        Dim procblock As DefineBlock
        procblock = DefineBlockParam("function", FunctionName)

        If procblock.StartLine = 0 And procblock.EndLine = 0 Then
            'Function does not exist; try an internal function.
            sIntFuncResult = DoInternalFunction(FunctionName, FunctionParameter, ctx)
            If sIntFuncResult = "__NOTDEFINED" Then
                LogASLError("No such function '" & FunctionName & "'", LogType.WarningError)
                Return "[ERROR]"
            Else
                bIntFuncExecuted = True
            End If
        End If

        Dim iNumParameters, iCurPos As Integer
        If bIntFuncExecuted Then
            Return sIntFuncResult
        Else
            Dim NewThread As Context = CopyContext(ctx)

            iNumParameters = 0 : iCurPos = 1

            If FunctionParameter <> "" Then
                FunctionParameter = FunctionParameter & ";"
                Do
                    iNumParameters = iNumParameters + 1
                    SCP = InStr(iCurPos, FunctionParameter, ";")

                    ParameterData = Trim(Mid(FunctionParameter, iCurPos, SCP - iCurPos))
                    SetStringContents("quest.function.parameter." & Trim(Str(iNumParameters)), ParameterData, ctx)

                    NewThread.NumParameters = iNumParameters
                    ReDim Preserve NewThread.Parameters(iNumParameters)
                    NewThread.Parameters(iNumParameters) = ParameterData

                    iCurPos = SCP + 1
                Loop Until iCurPos >= Len(FunctionParameter)
                SetStringContents("quest.function.numparameters", Trim(Str(iNumParameters)), ctx)
            Else
                SetStringContents("quest.function.numparameters", "0", ctx)
                NewThread.NumParameters = 0
            End If

            Dim result As String = ""

            For i = procblock.StartLine + 1 To procblock.EndLine - 1
                ExecuteScript(_lines(i), NewThread)
                result = NewThread.FunctionReturnValue
            Next i

            Return result
        End If

    End Function

    Private Function DoInternalFunction(FunctionName As String, FunctionParameter As String, ctx As Context) As String
        ' Split FunctionParameter into individual parameters
        Dim iNumParameters, iCurPos As Integer
        Dim Parameter() As String
        Dim UntrimmedParameter() As String
        Dim SCP, ObjID, i As Integer
        Dim FoundObj As Boolean

        iNumParameters = 0 : iCurPos = 1

        If FunctionParameter <> "" Then
            FunctionParameter = FunctionParameter & ";"
            Do
                iNumParameters = iNumParameters + 1
                SCP = InStr(iCurPos, FunctionParameter, ";")
                ReDim Preserve Parameter(iNumParameters)
                ReDim Preserve UntrimmedParameter(iNumParameters)

                UntrimmedParameter(iNumParameters) = Mid(FunctionParameter, iCurPos, SCP - iCurPos)
                Parameter(iNumParameters) = Trim(UntrimmedParameter(iNumParameters))

                iCurPos = SCP + 1
            Loop Until iCurPos >= Len(FunctionParameter)

            ' Remove final ";"
            FunctionParameter = Left(FunctionParameter, Len(FunctionParameter) - 1)
        Else
            iNumParameters = 1
            ReDim Parameter(1)
            ReDim UntrimmedParameter(1)
            Parameter(1) = ""
            UntrimmedParameter(1) = ""
        End If

        Dim Param3 As String
        Dim Param2 As String
        Dim oExit As RoomExit
        If FunctionName = "displayname" Then
            ObjID = GetObjectID(Parameter(1), ctx)
            If ObjID = -1 Then
                LogASLError("Object '" & Parameter(1) & "' does not exist", LogType.WarningError)
                Return "!"
            Else
                Return _objs(GetObjectID(Parameter(1), ctx)).ObjectAlias
            End If
        ElseIf FunctionName = "numberparameters" Then
            Return Trim(Str(ctx.NumParameters))
        ElseIf FunctionName = "parameter" Then
            If iNumParameters = 0 Then
                LogASLError("No parameter number specified for $parameter$ function", LogType.WarningError)
                Return ""
            Else
                If Val(Parameter(1)) > ctx.NumParameters Then
                    LogASLError("No parameter number " & Parameter(1) & " sent to this function", LogType.WarningError)
                    Return ""
                Else
                    Return Trim(ctx.Parameters(CInt(Parameter(1))))
                End If
            End If
        ElseIf FunctionName = "gettag" Then
            ' Deprecated
            Return FindStatement(DefineBlockParam("room", Parameter(1)), Parameter(2))
        ElseIf FunctionName = "objectname" Then
            Return _objs(ctx.CallingObjectId).ObjectName
        ElseIf FunctionName = "locationof" Then
            For i = 1 To _numberChars
                If LCase(_chars(i).ObjectName) = LCase(Parameter(1)) Then
                    Return _chars(i).ContainerRoom
                End If
            Next i

            For i = 1 To _numberObjs
                If LCase(_objs(i).ObjectName) = LCase(Parameter(1)) Then
                    Return _objs(i).ContainerRoom
                End If
            Next i
        ElseIf FunctionName = "lengthof" Then
            Return Str(Len(UntrimmedParameter(1)))
        ElseIf FunctionName = "left" Then
            If Val(Parameter(2)) < 0 Then
                LogASLError("Invalid function call in '$Left$(" & Parameter(1) & "; " & Parameter(2) & ")$'", LogType.WarningError)
                Return "!"
            Else
                Return Left(Parameter(1), CInt(Parameter(2)))
            End If
        ElseIf FunctionName = "right" Then
            If Val(Parameter(2)) < 0 Then
                LogASLError("Invalid function call in '$Right$(" & Parameter(1) & "; " & Parameter(2) & ")$'", LogType.WarningError)
                Return "!"
            Else
                Return Right(Parameter(1), CInt(Parameter(2)))
            End If
        ElseIf FunctionName = "mid" Then
            If iNumParameters = 3 Then
                If Val(Parameter(2)) < 0 Then
                    LogASLError("Invalid function call in '$Mid$(" & Parameter(1) & "; " & Parameter(2) & "; " & Parameter(3) & ")$'", LogType.WarningError)
                    Return "!"
                Else
                    Return Mid(Parameter(1), CInt(Parameter(2)), CInt(Parameter(3)))
                End If
            ElseIf iNumParameters = 2 Then
                If Val(Parameter(2)) < 0 Then
                    LogASLError("Invalid function call in '$Mid$(" & Parameter(1) & "; " & Parameter(2) & ")$'", LogType.WarningError)
                    Return "!"
                Else
                    Return Mid(Parameter(1), CInt(Parameter(2)))
                End If
            End If
            LogASLError("Invalid function call to '$Mid$(...)$'", LogType.WarningError)
            Return ""
        ElseIf FunctionName = "rand" Then
            Return Str(Int(_random.NextDouble() * (CDbl(Parameter(2)) - CDbl(Parameter(1)) + 1)) + CDbl(Parameter(1)))
        ElseIf FunctionName = "instr" Then
            If iNumParameters = 3 Then
                Param3 = ""
                If InStr(Parameter(3), "_") <> 0 Then
                    For i = 1 To Len(Parameter(3))
                        If Mid(Parameter(3), i, 1) = "_" Then
                            Param3 = Param3 & " "
                        Else
                            Param3 = Param3 & Mid(Parameter(3), i, 1)
                        End If
                    Next i
                Else
                    Param3 = Parameter(3)
                End If
                If Val(Parameter(1)) <= 0 Then
                    LogASLError("Invalid function call in '$instr(" & Parameter(1) & "; " & Parameter(2) & "; " & Parameter(3) & ")$'", LogType.WarningError)
                    Return "!"
                Else
                    Return Trim(Str(InStr(CInt(Parameter(1)), Parameter(2), Param3)))
                End If
            ElseIf iNumParameters = 2 Then
                Param2 = ""
                If InStr(Parameter(2), "_") <> 0 Then
                    For i = 1 To Len(Parameter(2))
                        If Mid(Parameter(2), i, 1) = "_" Then
                            Param2 = Param2 & " "
                        Else
                            Param2 = Param2 & Mid(Parameter(2), i, 1)
                        End If
                    Next i
                Else
                    Param2 = Parameter(2)
                End If
                Return Trim(Str(InStr(Parameter(1), Param2)))
            End If
            LogASLError("Invalid function call to '$Instr$(...)$'", LogType.WarningError)
            Return ""
        ElseIf FunctionName = "ucase" Then
            Return UCase(Parameter(1))
        ElseIf FunctionName = "lcase" Then
            Return LCase(Parameter(1))
        ElseIf FunctionName = "capfirst" Then
            Return UCase(Left(Parameter(1), 1)) & Mid(Parameter(1), 2)
        ElseIf FunctionName = "symbol" Then
            If Parameter(1) = "lt" Then
                Return "<"
            ElseIf Parameter(1) = "gt" Then
                Return ">"
            Else
                Return "!"
            End If
        ElseIf FunctionName = "loadmethod" Then
            Return _gameLoadMethod
        ElseIf FunctionName = "timerstate" Then
            For i = 1 To _numberTimers
                If LCase(_timers(i).TimerName) = LCase(Parameter(1)) Then
                    If _timers(i).TimerActive Then
                        Return "1"
                    Else
                        Return "0"
                    End If
                End If
            Next i
            LogASLError("No such timer '" & Parameter(1) & "'", LogType.WarningError)
            Return "!"
        ElseIf FunctionName = "timerinterval" Then
            For i = 1 To _numberTimers
                If LCase(_timers(i).TimerName) = LCase(Parameter(1)) Then
                    Return Str(_timers(i).TimerInterval)
                End If
            Next i
            LogASLError("No such timer '" & Parameter(1) & "'", LogType.WarningError)
            Return "!"
        ElseIf FunctionName = "ubound" Then
            For i = 1 To _numberNumericVariables
                If LCase(_numericVariable(i).VariableName) = LCase(Parameter(1)) Then
                    Return Trim(Str(_numericVariable(i).VariableUBound))
                End If
            Next i

            For i = 1 To _numberStringVariables
                If LCase(_stringVariable(i).VariableName) = LCase(Parameter(1)) Then
                    Return Trim(Str(_stringVariable(i).VariableUBound))
                End If
            Next i

            LogASLError("No such variable '" & Parameter(1) & "'", LogType.WarningError)
            Return "!"
        ElseIf FunctionName = "objectproperty" Then
            FoundObj = False
            For i = 1 To _numberObjs
                If LCase(_objs(i).ObjectName) = LCase(Parameter(1)) Then
                    FoundObj = True
                    ObjID = i
                    i = _numberObjs
                End If
            Next i

            If Not FoundObj Then
                LogASLError("No such object '" & Parameter(1) & "'", LogType.WarningError)
                Return "!"
            Else
                Return GetObjectProperty(Parameter(2), ObjID)
            End If
        ElseIf FunctionName = "getobjectname" Then
            If iNumParameters = 3 Then
                ObjID = Disambiguate(Parameter(1), Parameter(2) & ";" & Parameter(3), ctx)
            ElseIf iNumParameters = 2 Then
                ObjID = Disambiguate(Parameter(1), Parameter(2), ctx)
            Else

                ObjID = Disambiguate(Parameter(1), _currentRoom & ";inventory", ctx)
            End If

            If ObjID <= -1 Then
                LogASLError("No object found with display name '" & Parameter(1) & "'", LogType.WarningError)
                Return "!"
            Else
                Return _objs(ObjID).ObjectName
            End If
        ElseIf FunctionName = "thisobject" Then
            Return _objs(ctx.CallingObjectId).ObjectName
        ElseIf FunctionName = "thisobjectname" Then
            Return _objs(ctx.CallingObjectId).ObjectAlias
        ElseIf FunctionName = "speechenabled" Then
            Return "1"
        ElseIf FunctionName = "removeformatting" Then
            Return StripCodes(FunctionParameter)
        ElseIf FunctionName = "findexit" And _gameAslVersion >= 410 Then
            oExit = FindExit(FunctionParameter)
            If oExit Is Nothing Then
                Return ""
            Else
                Return _objs(oExit.ObjID).ObjectName
            End If
        End If

        Return "__NOTDEFINED"

    End Function

    Private Sub ExecFor(ScriptLine As String, ctx As Context)
        ' See if this is a "for each" loop:
        If BeginsWith(ScriptLine, "each ") Then
            ExecForEach(GetEverythingAfter(ScriptLine, "each "), ctx)
            Exit Sub
        End If

        ' Executes a for loop, of form:
        '   for <variable; startvalue; endvalue> script
        Dim CounterVariable As String
        Dim StartValue As Integer
        Dim EndValue As Integer
        Dim LoopScript As String
        Dim StepValue As Integer
        Dim ForData As String
        Dim SCPos2, SCPos1, SCPos3 As Integer
        Dim i As Double

        ForData = RetrieveParameter(ScriptLine, ctx)

        ' Extract individual components:
        SCPos1 = InStr(ForData, ";")
        SCPos2 = InStr(SCPos1 + 1, ForData, ";")
        SCPos3 = InStr(SCPos2 + 1, ForData, ";")

        CounterVariable = Trim(Left(ForData, SCPos1 - 1))
        StartValue = CInt(Mid(ForData, SCPos1 + 1, (SCPos2 - 1) - SCPos1))

        If SCPos3 <> 0 Then
            EndValue = CInt(Mid(ForData, SCPos2 + 1, (SCPos3 - 1) - SCPos2))
            StepValue = CInt(Mid(ForData, SCPos3 + 1))
        Else
            EndValue = CInt(Mid(ForData, SCPos2 + 1))
            StepValue = 1
        End If

        LoopScript = Trim(Mid(ScriptLine, InStr(ScriptLine, ">") + 1))

        For i = StartValue To EndValue Step StepValue
            SetNumericVariableContents(CounterVariable, i, ctx)
            ExecuteScript(LoopScript, ctx)
            i = GetNumericContents(CounterVariable, ctx)
        Next i

    End Sub

    Private Sub ExecSetVar(VarInfo As String, ctx As Context)
        ' Sets variable contents from a script parameter.
        ' Eg <var1;7> sets numeric variable var1
        ' to 7

        Dim iSemiColonPos As Integer
        Dim sVarName As String
        Dim iVarCont As String
        Dim ArrayIndex As Integer
        Dim FirstNum, SecondNum As Double
        Dim ObscuredVarInfo As String
        Dim ExpResult As ExpressionResult
        Dim OpPos As Integer
        Dim OP As String

        iSemiColonPos = InStr(VarInfo, ";")
        sVarName = Trim(Left(VarInfo, iSemiColonPos - 1))
        iVarCont = Trim(Mid(VarInfo, iSemiColonPos + 1))

        ArrayIndex = GetArrayIndex(sVarName, ctx)

        If IsNumeric(sVarName) Then
            LogASLError("Invalid numeric variable name '" & sVarName & "' - variable names cannot be numeric", LogType.WarningError)
            Exit Sub
        End If

        Try
            If _gameAslVersion >= 391 Then
                ExpResult = ExpressionHandler(iVarCont)
                If ExpResult.Success = ExpressionSuccess.OK Then
                    iVarCont = ExpResult.Result
                Else
                    iVarCont = "0"
                    LogASLError("Error setting numeric variable <" & VarInfo & "> : " & ExpResult.Message, LogType.WarningError)
                End If
            Else
                ObscuredVarInfo = ObscureNumericExps(iVarCont)

                OpPos = InStr(ObscuredVarInfo, "+")
                If OpPos = 0 Then OpPos = InStr(ObscuredVarInfo, "*")
                If OpPos = 0 Then OpPos = InStr(ObscuredVarInfo, "/")
                If OpPos = 0 Then OpPos = InStr(2, ObscuredVarInfo, "-")

                If OpPos <> 0 Then
                    OP = Mid(iVarCont, OpPos, 1)
                    FirstNum = Val(Left(iVarCont, OpPos - 1))
                    SecondNum = Val(Mid(iVarCont, OpPos + 1))

                    Select Case OP
                        Case "+"
                            iVarCont = Str(FirstNum + SecondNum)
                        Case "-"
                            iVarCont = Str(FirstNum - SecondNum)
                        Case "*"
                            iVarCont = Str(FirstNum * SecondNum)
                        Case "/"
                            If SecondNum <> 0 Then
                                iVarCont = Str(FirstNum / SecondNum)
                            Else
                                LogASLError("Division by zero - The result of this operation has been set to zero.", LogType.WarningError)
                                iVarCont = "0"
                            End If
                    End Select
                End If
            End If

            SetNumericVariableContents(sVarName, Val(iVarCont), ctx, ArrayIndex)
        Catch
            LogASLError("Error " & Trim(CStr(Err.Number)) & " (" & Err.Description & ") setting variable '" & sVarName & "' to '" & iVarCont & "'", LogType.WarningError)
        End Try
    End Sub

    Private m_questionResponse As Boolean

    Private Function ExecuteIfAsk(askq As String) As Boolean
        m_player.ShowQuestion(askq)
        ChangeState(State.Waiting)

        SyncLock _waitLock
            System.Threading.Monitor.Wait(_waitLock)
        End SyncLock

        Return m_questionResponse
    End Function

    Private Sub SetQuestionResponse(response As Boolean) Implements IASL.SetQuestionResponse
        Dim runnerThread As New System.Threading.Thread(New System.Threading.ParameterizedThreadStart(AddressOf SetQuestionResponseInNewThread))
        ChangeState(State.Working)
        runnerThread.Start(response)
        WaitForStateChange(State.Working)
    End Sub

    Private Sub SetQuestionResponseInNewThread(response As Object)
        m_questionResponse = DirectCast(response, Boolean)

        SyncLock _waitLock
            System.Threading.Monitor.PulseAll(_waitLock)
        End SyncLock
    End Sub

    Private Function ExecuteIfGot(theitem As String) As Boolean
        Dim i As Integer

        Dim FoundObject As Boolean
        Dim result As Boolean
        Dim InventoryPlace As String
        Dim valid As Boolean

        If _gameAslVersion >= 280 Then
            FoundObject = False
            result = False
            InventoryPlace = "inventory"

            For i = 1 To _numberObjs
                If LCase(_objs(i).ObjectName) = LCase(theitem) Then
                    FoundObject = True
                    result = _objs(i).ContainerRoom = InventoryPlace And _objs(i).Exists
                End If
            Next i

            If Not FoundObject Then
                result = False
                LogASLError("No object '" & theitem & "' defined.", LogType.WarningError)
            End If

            Return result
        End If

        valid = False

        For i = 1 To _numberItems
            If LCase(_items(i).Name) = LCase(theitem) Then
                result = _items(i).Got
                i = _numberItems
                valid = True
            End If
        Next i

        If Not valid Then
            LogASLError("Item '" & theitem & "' not defined.", LogType.WarningError)
            result = False
        End If

        Return result
    End Function

    Private Function ExecuteIfHas(hascond As String) As Boolean

        Dim checkval As Double
        Dim condresult As Boolean
        Dim SemiColonPos As Integer
        Dim TheNewVal, CName, OP As String
        Dim i, ColNum As Integer
        Dim TheNewValue As String

        SemiColonPos = InStr(hascond, ";")
        CName = Trim(Left(hascond, SemiColonPos - 1))
        TheNewVal = Trim(Right(hascond, Len(hascond) - SemiColonPos))

        i = -1

        For i = 1 To _numCollectables
            If _collectables(i).Name = CName Then
                ColNum = i
                i = _numCollectables
            End If
        Next i

        If i = -1 Then
            LogASLError("No such collectable in " & hascond, LogType.WarningError)
            Exit Function
        End If

        OP = Left(TheNewVal, 1)
        TheNewValue = Trim(Right(TheNewVal, Len(TheNewVal) - 1))
        If IsNumeric(TheNewValue) Then
            checkval = Val(TheNewValue)
        Else
            checkval = GetCollectableAmount(TheNewValue)
        End If

        If OP = "+" Then
            If _collectables(ColNum).Value > checkval Then condresult = True Else condresult = False
        ElseIf OP = "-" Then
            If _collectables(ColNum).Value < checkval Then condresult = True Else condresult = False
        ElseIf OP = "=" Then
            If _collectables(ColNum).Value = checkval Then condresult = True Else condresult = False
        End If

        Return condresult
    End Function

    Private Function ExecuteIfIs(IsCondition As String) As Boolean
        Dim SCPos, SC2Pos As Integer
        Dim Value1, Value2 As String
        Dim Condition As String
        Dim Satisfied As Boolean
        Dim ExpectNumerics As Boolean
        Dim ExpResult As ExpressionResult

        SCPos = InStr(IsCondition, ";")
        If SCPos = 0 Then
            LogASLError("Expected second parameter in 'is " & IsCondition & "'", LogType.WarningError)
            Return False
        End If

        SC2Pos = InStr(SCPos + 1, IsCondition, ";")
        If SC2Pos = 0 Then
            ' Only two parameters => standard "="
            Condition = "="
            Value1 = Trim(Left(IsCondition, SCPos - 1))
            Value2 = Trim(Mid(IsCondition, SCPos + 1))
        Else
            Value1 = Trim(Left(IsCondition, SCPos - 1))
            Condition = Trim(Mid(IsCondition, SCPos + 1, (SC2Pos - SCPos) - 1))
            Value2 = Trim(Mid(IsCondition, SC2Pos + 1))
        End If

        If _gameAslVersion >= 391 Then
            ' Evaluate expressions in Value1 and Value2
            ExpResult = ExpressionHandler(Value1)

            If ExpResult.Success = ExpressionSuccess.OK Then
                Value1 = ExpResult.Result
            End If

            ExpResult = ExpressionHandler(Value2)

            If ExpResult.Success = ExpressionSuccess.OK Then
                Value2 = ExpResult.Result
            End If
        End If

        Satisfied = False

        Select Case Condition
            Case "="
                If LCase(Value1) = LCase(Value2) Then
                    Satisfied = True
                End If
                ExpectNumerics = False
            Case "!="
                If LCase(Value1) <> LCase(Value2) Then
                    Satisfied = True
                End If
                ExpectNumerics = False
            Case "gt"
                If Val(Value1) > Val(Value2) Then
                    Satisfied = True
                End If
                ExpectNumerics = True
            Case "lt"
                If Val(Value1) < Val(Value2) Then
                    Satisfied = True
                End If
                ExpectNumerics = True
            Case "gt="
                If Val(Value1) >= Val(Value2) Then
                    Satisfied = True
                End If
                ExpectNumerics = True
            Case "lt="
                If Val(Value1) <= Val(Value2) Then
                    Satisfied = True
                End If
                ExpectNumerics = True
            Case Else
                LogASLError("Unrecognised comparison condition in 'is " & IsCondition & "'", LogType.WarningError)
        End Select

        If ExpectNumerics Then
            If Not (IsNumeric(Value1) And IsNumeric(Value2)) Then
                LogASLError("Expected numeric comparison comparing '" & Value1 & "' and '" & Value2 & "'", LogType.WarningError)
            End If
        End If

        Return Satisfied
    End Function

    Private Function GetNumericContents(NumericName As String, ctx As Context, Optional NOERROR As Boolean = False) As Double
        Dim bNumExists As Boolean
        Dim iNumNumber As Integer
        Dim ArrayIndex, i As Integer
        Dim ArrayIndexData As String
        bNumExists = False

        ' First, see if the variable already exists. If it
        ' does, get its contents. If not, generate an error.

        Dim OpenPos, ClosePos As Integer
        If InStr(NumericName, "[") <> 0 And InStr(NumericName, "]") <> 0 Then
            OpenPos = InStr(NumericName, "[")
            ClosePos = InStr(NumericName, "]")
            ArrayIndexData = Mid(NumericName, OpenPos + 1, (ClosePos - OpenPos) - 1)
            If IsNumeric(ArrayIndexData) Then
                ArrayIndex = CInt(ArrayIndexData)
            Else
                ArrayIndex = CInt(GetNumericContents(ArrayIndexData, ctx))
            End If
            NumericName = Left(NumericName, OpenPos - 1)
        Else
            ArrayIndex = 0
        End If

        If _numberNumericVariables > 0 Then
            For i = 1 To _numberNumericVariables
                If LCase(_numericVariable(i).VariableName) = LCase(NumericName) Then
                    iNumNumber = i
                    bNumExists = True
                    i = _numberNumericVariables
                End If
            Next i
        End If

        If bNumExists = False Then
            If Not NOERROR Then LogASLError("No numeric variable '" & NumericName & "' defined.", LogType.WarningError)
            Return -32767
        End If

        If ArrayIndex > _numericVariable(iNumNumber).VariableUBound Then
            If Not NOERROR Then LogASLError("Array index of '" & NumericName & "[" & Trim(Str(ArrayIndex)) & "]' too big.", LogType.WarningError)
            Return -32766
        End If

        ' Now, set the contents
        Return Val(_numericVariable(iNumNumber).VariableContents(ArrayIndex))
    End Function

    Friend Sub PlayerErrorMessage(e As PlayerError, ctx As Context)
        Print(GetErrorMessage(e, ctx), ctx)
    End Sub

    Private Sub PlayerErrorMessage_ExtendInfo(e As PlayerError, ctx As Context, sExtraInfo As String)
        Dim sErrorMessage As String

        sErrorMessage = GetErrorMessage(e, ctx)

        If sExtraInfo <> "" Then
            If Right(sErrorMessage, 1) = "." Then
                sErrorMessage = Left(sErrorMessage, Len(sErrorMessage) - 1)
            End If

            sErrorMessage = sErrorMessage & " - " & sExtraInfo & "."
        End If

        Print(sErrorMessage, ctx)
    End Sub

    Private Function GetErrorMessage(e As PlayerError, ctx As Context) As String
        Return ConvertParameter(ConvertParameter(ConvertParameter(_playerErrorMessageString(e), "%", ConvertType.Numeric, ctx), "$", ConvertType.Functions, ctx), "#", ConvertType.Strings, ctx)
    End Function

    Private Sub PlayMedia(filename As String)
        PlayMedia(filename, False, False)
    End Sub

    Private Sub PlayMedia(filename As String, sync As Boolean, looped As Boolean)
        If filename.Length = 0 Then
            m_player.StopSound()
        Else
            If looped And sync Then sync = False ' Can't loop and sync at the same time, that would just hang!

            m_player.PlaySound(filename, sync, looped)

            If sync Then
                ChangeState(State.Waiting)
            End If

            If sync Then
                SyncLock (_waitLock)
                    System.Threading.Monitor.Wait(_waitLock)
                End SyncLock
            End If

        End If
    End Sub

    Private Sub PlayWav(parameter As String)
        Dim sync As Boolean = False
        Dim looped As Boolean = False
        Dim filename As String

        Dim params As New List(Of String)(parameter.Split(";"c))

        params = New List(Of String)(params.Select(Function(p As String) Trim(p)))

        filename = params(0)

        If params.Contains("loop") Then looped = True
        If params.Contains("sync") Then sync = True

        If filename.Length > 0 And InStr(filename, ".") = 0 Then
            filename = filename & ".wav"
        End If

        PlayMedia(filename, sync, looped)
    End Sub

    Private Sub RestoreGameData(InputFileData As String)
        Dim i, NumData, j As Integer
        Dim AppliesTo As String
        Dim data As String = ""
        Dim ObjID, TimerNum As Integer
        Dim VarUBound As Integer
        Dim Found As Boolean
        Dim NumStoredData As Integer
        Dim StoredData(0) As ChangeType
        Dim DecryptedFile As New System.Text.StringBuilder

        ' Decrypt file
        For i = 1 To Len(InputFileData)
            DecryptedFile.Append(Chr(255 - Asc(Mid(InputFileData, i, 1))))
        Next i

        m_sFileData = DecryptedFile.ToString()

        _currentRoom = GetNextChunk()

        ' OBJECTS

        NumData = CInt(GetNextChunk())

        Dim createdObjects As New List(Of String)

        For i = 1 To NumData
            AppliesTo = GetNextChunk()
            data = GetNextChunk()

            ' As of Quest 4.0, properties and actions are put into StoredData while we load the file,
            ' and then processed later. This is so any created rooms pick up their properties - otherwise
            ' we try to set them before they've been created.

            If BeginsWith(data, "properties ") Or BeginsWith(data, "action ") Then
                NumStoredData = NumStoredData + 1
                ReDim Preserve StoredData(NumStoredData)
                StoredData(NumStoredData) = New ChangeType
                StoredData(NumStoredData).AppliesTo = AppliesTo
                StoredData(NumStoredData).Change = data
            ElseIf BeginsWith(data, "create ") Then
                Dim createData As String = AppliesTo & ";" & GetEverythingAfter(data, "create ")
                ' workaround bug where duplicate "create" entries appear in the restore data
                If Not createdObjects.Contains(createData) Then
                    ExecuteCreate("object <" & createData & ">", _nullContext)
                    createdObjects.Add(createData)
                End If
            Else
                LogASLError("QSG Error: Unrecognised item '" & AppliesTo & "; " & data & "'", LogType.InternalError)
            End If
        Next i

        NumData = CInt(GetNextChunk())
        For i = 1 To NumData
            AppliesTo = GetNextChunk()
            data = GetFileDataChars(2)

            ObjID = GetObjectIDNoAlias(AppliesTo)

            If Left(data, 1) = Chr(1) Then
                _objs(ObjID).Exists = True
            Else
                _objs(ObjID).Exists = False
            End If

            If Right(data, 1) = Chr(1) Then
                _objs(ObjID).Visible = True
            Else
                _objs(ObjID).Visible = False
            End If

            _objs(ObjID).ContainerRoom = GetNextChunk()
        Next i

        ' ROOMS

        NumData = CInt(GetNextChunk())

        For i = 1 To NumData
            AppliesTo = GetNextChunk()
            data = GetNextChunk()

            If BeginsWith(data, "exit ") Then
                ExecuteCreate(data, _nullContext)
            ElseIf data = "create" Then
                ExecuteCreate("room <" & AppliesTo & ">", _nullContext)
            ElseIf BeginsWith(data, "destroy exit ") Then
                DestroyExit(AppliesTo & "; " & GetEverythingAfter(data, "destroy exit "), _nullContext)
            End If
        Next i

        ' Now go through and apply object properties and actions

        For i = 1 To NumStoredData
            Dim d = StoredData(i)
            If BeginsWith(d.Change, "properties ") Then
                AddToObjectProperties(GetEverythingAfter(d.Change, "properties "), GetObjectIDNoAlias(d.AppliesTo), _nullContext)
            ElseIf BeginsWith(d.Change, "action ") Then
                AddToObjectActions(GetEverythingAfter(d.Change, "action "), GetObjectIDNoAlias(d.AppliesTo), _nullContext)
            End If
        Next i

        ' TIMERS

        NumData = CInt(GetNextChunk())
        For i = 1 To NumData
            Found = False
            AppliesTo = GetNextChunk()
            For j = 1 To _numberTimers
                If _timers(j).TimerName = AppliesTo Then
                    TimerNum = j
                    j = _numberTimers
                    Found = True
                End If
            Next j

            If Found Then
                Dim t = _timers(TimerNum)
                Dim thisChar As String = GetFileDataChars(1)

                If thisChar = Chr(1) Then
                    t.TimerActive = True
                Else
                    t.TimerActive = False
                End If

                t.TimerInterval = CInt(GetNextChunk())
                t.TimerTicks = CInt(GetNextChunk())
            End If
        Next i

        ' STRING VARIABLES

        ' Set this flag so we don't run any status variable onchange scripts while restoring
        m_gameIsRestoring = True

        NumData = CInt(GetNextChunk())
        For i = 1 To NumData
            AppliesTo = GetNextChunk()
            VarUBound = CInt(GetNextChunk())

            If VarUBound = 0 Then
                data = GetNextChunk()
                SetStringContents(AppliesTo, data, _nullContext)
            Else
                For j = 0 To VarUBound
                    data = GetNextChunk()
                    SetStringContents(AppliesTo, data, _nullContext, j)
                Next j
            End If
        Next i

        ' NUMERIC VARIABLES

        NumData = CInt(GetNextChunk())
        For i = 1 To NumData
            AppliesTo = GetNextChunk()
            VarUBound = CInt(GetNextChunk())

            If VarUBound = 0 Then
                data = GetNextChunk()
                SetNumericVariableContents(AppliesTo, Val(data), _nullContext)
            Else
                For j = 0 To VarUBound
                    data = GetNextChunk()
                    SetNumericVariableContents(AppliesTo, Val(data), _nullContext, j)
                Next j
            End If
        Next i

        m_gameIsRestoring = False
    End Sub

    Private Sub SetBackground(Colour As String)
        m_player.SetBackground("#" + GetHTMLColour(Colour, "white"))
    End Sub

    Private Sub SetForeground(Colour As String)
        m_player.SetForeground("#" + GetHTMLColour(Colour, "black"))
    End Sub

    Private Sub SetDefaultPlayerErrorMessages()
        _playerErrorMessageString(PlayerError.BadCommand) = "I don't understand your command. Type HELP for a list of valid commands."
        _playerErrorMessageString(PlayerError.BadGo) = "I don't understand your use of 'GO' - you must either GO in some direction, or GO TO a place."
        _playerErrorMessageString(PlayerError.BadGive) = "You didn't say who you wanted to give that to."
        _playerErrorMessageString(PlayerError.BadCharacter) = "I can't see anybody of that name here."
        _playerErrorMessageString(PlayerError.NoItem) = "You don't have that."
        _playerErrorMessageString(PlayerError.ItemUnwanted) = "#quest.error.gender# doesn't want #quest.error.article#."
        _playerErrorMessageString(PlayerError.BadLook) = "You didn't say what you wanted to look at."
        _playerErrorMessageString(PlayerError.BadThing) = "I can't see that here."
        _playerErrorMessageString(PlayerError.DefaultLook) = "Nothing out of the ordinary."
        _playerErrorMessageString(PlayerError.DefaultSpeak) = "#quest.error.gender# says nothing."
        _playerErrorMessageString(PlayerError.BadItem) = "I can't see that anywhere."
        _playerErrorMessageString(PlayerError.DefaultTake) = "You pick #quest.error.article# up."
        _playerErrorMessageString(PlayerError.BadUse) = "You didn't say what you wanted to use that on."
        _playerErrorMessageString(PlayerError.DefaultUse) = "You can't use that here."
        _playerErrorMessageString(PlayerError.DefaultOut) = "There's nowhere you can go out to around here."
        _playerErrorMessageString(PlayerError.BadPlace) = "You can't go there."
        _playerErrorMessageString(PlayerError.DefaultExamine) = "Nothing out of the ordinary."
        _playerErrorMessageString(PlayerError.BadTake) = "You can't take #quest.error.article#."
        _playerErrorMessageString(PlayerError.CantDrop) = "You can't drop that here."
        _playerErrorMessageString(PlayerError.DefaultDrop) = "You drop #quest.error.article#."
        _playerErrorMessageString(PlayerError.BadDrop) = "You are not carrying such a thing."
        _playerErrorMessageString(PlayerError.BadPronoun) = "I don't know what '#quest.error.pronoun#' you are referring to."
        _playerErrorMessageString(PlayerError.BadExamine) = "You didn't say what you wanted to examine."
        _playerErrorMessageString(PlayerError.AlreadyOpen) = "It is already open."
        _playerErrorMessageString(PlayerError.AlreadyClosed) = "It is already closed."
        _playerErrorMessageString(PlayerError.CantOpen) = "You can't open that."
        _playerErrorMessageString(PlayerError.CantClose) = "You can't close that."
        _playerErrorMessageString(PlayerError.DefaultOpen) = "You open it."
        _playerErrorMessageString(PlayerError.DefaultClose) = "You close it."
        _playerErrorMessageString(PlayerError.BadPut) = "You didn't specify what you wanted to put #quest.error.article# on or in."
        _playerErrorMessageString(PlayerError.CantPut) = "You can't put that there."
        _playerErrorMessageString(PlayerError.DefaultPut) = "Done."
        _playerErrorMessageString(PlayerError.CantRemove) = "You can't remove that."
        _playerErrorMessageString(PlayerError.AlreadyPut) = "It is already there."
        _playerErrorMessageString(PlayerError.DefaultRemove) = "Done."
        _playerErrorMessageString(PlayerError.Locked) = "The exit is locked."
        _playerErrorMessageString(PlayerError.DefaultWait) = "Press a key to continue..."
        _playerErrorMessageString(PlayerError.AlreadyTaken) = "You already have that."
    End Sub

    Private Sub SetFont(FontName As String, ctx As Context, Optional OutputTo As String = "normal")
        If FontName = "" Then FontName = _defaultFontName
        m_player.SetFont(FontName)
    End Sub

    Private Sub SetFontSize(FontSize As Double, ctx As Context, Optional OutputTo As String = "normal")
        If FontSize = 0 Then FontSize = _defaultFontSize
        m_player.SetFontSize(CStr(FontSize))
    End Sub

    Private Sub SetNumericVariableContents(NumName As String, NumContent As Double, ctx As Context, Optional ArrayIndex As Integer = 0)
        Dim bNumExists As Boolean
        Dim iNumNumber As Integer
        Dim NumTitle, OnChangeScript As String
        Dim i As Integer
        bNumExists = False

        If IsNumeric(NumName) Then
            LogASLError("Illegal numeric variable name '" & NumName & "' - check you didn't put % around the variable name in the ASL code", LogType.WarningError)
            Exit Sub
        End If

        ' First, see if variable already exists. If it does,
        ' modify it. If not, create it.

        If _numberNumericVariables > 0 Then
            For i = 1 To _numberNumericVariables
                If LCase(_numericVariable(i).VariableName) = LCase(NumName) Then
                    iNumNumber = i
                    bNumExists = True
                    i = _numberNumericVariables
                End If
            Next i
        End If

        If bNumExists = False Then
            _numberNumericVariables = _numberNumericVariables + 1
            iNumNumber = _numberNumericVariables
            ReDim Preserve _numericVariable(iNumNumber)
            _numericVariable(iNumNumber) = New VariableType

            For i = 0 To ArrayIndex
                NumTitle = NumName
                If ArrayIndex <> 0 Then NumTitle = NumTitle & "[" & Trim(Str(i)) & "]"
            Next i
            _numericVariable(iNumNumber).VariableUBound = ArrayIndex
        End If

        If ArrayIndex > _numericVariable(iNumNumber).VariableUBound Then
            ReDim Preserve _numericVariable(iNumNumber).VariableContents(ArrayIndex)
            For i = _numericVariable(iNumNumber).VariableUBound + 1 To ArrayIndex
                NumTitle = NumName
                If ArrayIndex <> 0 Then NumTitle = NumTitle & "[" & Trim(Str(i)) & "]"
            Next i
            _numericVariable(iNumNumber).VariableUBound = ArrayIndex

        End If

        ' Now, set the contents
        _numericVariable(iNumNumber).VariableName = NumName
        ReDim Preserve _numericVariable(iNumNumber).VariableContents(_numericVariable(iNumNumber).VariableUBound)
        _numericVariable(iNumNumber).VariableContents(ArrayIndex) = CStr(NumContent)

        If _numericVariable(iNumNumber).OnChangeScript <> "" And Not m_gameIsRestoring Then
            OnChangeScript = _numericVariable(iNumNumber).OnChangeScript
            ExecuteScript(OnChangeScript, ctx)
        End If

        If _numericVariable(iNumNumber).DisplayString <> "" Then
            UpdateStatusVars(ctx)
        End If

    End Sub

    Private Sub SetOpenClose(ObjectName As String, DoOpen As Boolean, ctx As Context)
        Dim ObjID As Integer
        Dim CommandName As String

        If DoOpen Then
            CommandName = "open"
        Else
            CommandName = "close"
        End If

        ObjID = GetObjectIDNoAlias(ObjectName)
        If ObjID = 0 Then
            LogASLError("Invalid object name specified in '" & CommandName & " <" & ObjectName & ">", LogType.WarningError)
            Exit Sub
        End If

        DoOpenClose(ObjID, DoOpen, False, ctx)

    End Sub

    Private Sub SetTimerState(TimerName As String, TimerState As Boolean)
        Dim FoundTimer As Boolean
        Dim i As Integer

        For i = 1 To _numberTimers
            If LCase(TimerName) = LCase(_timers(i).TimerName) Then
                FoundTimer = True
                _timers(i).TimerActive = TimerState
                _timers(i).BypassThisTurn = True     ' don't trigger timer during the turn it was first enabled
                i = _numberTimers
            End If
        Next i

        If Not FoundTimer Then
            LogASLError("No such timer '" & TimerName & "'", LogType.WarningError)
            Exit Sub
        End If

    End Sub

    Private Function SetUnknownVariableType(VariableData As String, ctx As Context) As SetResult
        Dim SCP As Integer
        Dim VariableName As String
        Dim VariableContents As String
        Dim i As Integer

        SCP = InStr(VariableData, ";")
        If SCP = 0 Then
            Return SetResult.Error
        End If

        VariableName = Trim(Left(VariableData, SCP - 1))
        Dim BeginPos As Integer
        If InStr(VariableName, "[") <> 0 And InStr(VariableName, "]") <> 0 Then
            BeginPos = InStr(VariableName, "[")
            VariableName = Left(VariableName, BeginPos - 1)
        End If

        VariableContents = Trim(Mid(VariableData, SCP + 1))

        For i = 1 To _numberStringVariables
            If LCase(_stringVariable(i).VariableName) = LCase(VariableName) Then
                ExecSetString(VariableData, ctx)
                Return SetResult.Found
            End If
        Next i

        For i = 1 To _numberNumericVariables
            If LCase(_numericVariable(i).VariableName) = LCase(VariableName) Then
                ExecSetVar(VariableData, ctx)
                Return SetResult.Found
            End If
        Next i

        For i = 1 To _numCollectables
            If LCase(_collectables(i).Name) = LCase(VariableName) Then
                ExecuteSetCollectable(VariableData, ctx)
                Return SetResult.Found
            End If
        Next i

        Return SetResult.Unfound
    End Function

    Private Function SetUpChoiceForm(choicesection As String, ctx As Context) As String
        ' Returns script to execute from choice block
        Dim choiceblock As DefineBlock
        Dim P As String
        Dim i As Integer

        choiceblock = DefineBlockParam("selection", choicesection)
        P = FindStatement(choiceblock, "info")

        Dim menuOptions As New Dictionary(Of String, String)
        Dim menuScript As New Dictionary(Of String, String)

        For i = choiceblock.StartLine + 1 To choiceblock.EndLine - 1
            If BeginsWith(_lines(i), "choice ") Then
                menuOptions.Add(CStr(i), RetrieveParameter(_lines(i), ctx))
                menuScript.Add(CStr(i), Trim(Right(_lines(i), Len(_lines(i)) - InStr(_lines(i), ">"))))
            End If
        Next i

        Print("- |i" & P & "|xi", ctx)

        Dim mnu As New MenuData(P, menuOptions, False)
        Dim choice As String = ShowMenu(mnu)

        Print("- " & menuOptions(choice) & "|n", ctx)
        Return menuScript(choice)
    End Function

    Private Sub SetUpDefaultFonts()
        ' Sets up default fonts
        Dim ThisFontName As String
        Dim ThisFontSize, i As Integer

        Dim gameblock As DefineBlock
        gameblock = GetDefineBlock("game")

        _defaultFontName = "Arial"
        _defaultFontSize = 9

        For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
            If BeginsWith(_lines(i), "default fontname ") Then
                ThisFontName = RetrieveParameter(_lines(i), _nullContext)
                If ThisFontName <> "" Then
                    _defaultFontName = ThisFontName
                End If
            ElseIf BeginsWith(_lines(i), "default fontsize ") Then
                ThisFontSize = CInt(RetrieveParameter(_lines(i), _nullContext))
                If ThisFontSize <> 0 Then
                    _defaultFontSize = ThisFontSize
                End If
            End If
        Next i
    End Sub

    Private Sub SetUpDisplayVariables()
        Dim i As Integer
        Dim iStringNumber As Integer
        Dim ThisVariable As VariableType
        Dim ThisType, DisplayString As String
        Dim iNumNumber As Integer

        For i = GetDefineBlock("game").StartLine + 1 To GetDefineBlock("game").EndLine - 1
            If BeginsWith(_lines(i), "define variable ") Then

                ThisVariable = New VariableType
                ReDim ThisVariable.VariableContents(0)

                ThisVariable.VariableName = RetrieveParameter(_lines(i), _nullContext)
                ThisVariable.DisplayString = ""
                ThisVariable.NoZeroDisplay = False
                ThisVariable.OnChangeScript = ""
                ThisVariable.VariableContents(0) = ""
                ThisVariable.VariableUBound = 0

                ThisType = "numeric"

                Do
                    i = i + 1

                    If BeginsWith(_lines(i), "type ") Then
                        ThisType = GetEverythingAfter(_lines(i), "type ")
                        If ThisType <> "string" And ThisType <> "numeric" Then
                            LogASLError("Unrecognised variable type in variable '" & ThisVariable.VariableName & "' - type '" & ThisType & "'", LogType.WarningError)
                            Exit Do
                        End If
                    ElseIf BeginsWith(_lines(i), "onchange ") Then
                        ThisVariable.OnChangeScript = GetEverythingAfter(_lines(i), "onchange ")
                    ElseIf BeginsWith(_lines(i), "display ") Then
                        DisplayString = GetEverythingAfter(_lines(i), "display ")
                        If BeginsWith(DisplayString, "nozero ") Then
                            ThisVariable.NoZeroDisplay = True
                            DisplayString = GetEverythingAfter(DisplayString, "nozero ")
                        End If
                        ThisVariable.DisplayString = RetrieveParameter(_lines(i), _nullContext, False)
                    ElseIf BeginsWith(_lines(i), "value ") Then
                        ThisVariable.VariableContents(0) = RetrieveParameter(_lines(i), _nullContext)
                    End If

                Loop Until Trim(_lines(i)) = "end define"

                If ThisType = "string" Then
                    ' Create string variable
                    _numberStringVariables = _numberStringVariables + 1
                    iStringNumber = _numberStringVariables
                    ReDim Preserve _stringVariable(iStringNumber)
                    _stringVariable(iStringNumber) = ThisVariable

                    _numDisplayStrings = _numDisplayStrings + 1

                ElseIf ThisType = "numeric" Then
                    If ThisVariable.VariableContents(0) = "" Then ThisVariable.VariableContents(0) = CStr(0)
                    _numberNumericVariables = _numberNumericVariables + 1
                    iNumNumber = _numberNumericVariables
                    ReDim Preserve _numericVariable(iNumNumber)
                    _numericVariable(iNumNumber) = ThisVariable

                    _numDisplayNumerics = _numDisplayNumerics + 1

                End If
            End If
        Next i

    End Sub

    Private Sub SetUpGameObject()
        Dim i As Integer
        Dim PropertyData As PropertiesActions
        Dim NestBlock, k As Integer

        _numberObjs = 1
        ReDim _objs(1)
        _objs(1) = New ObjectType
        Dim o = _objs(1)
        o.ObjectName = "game"
        o.ObjectAlias = ""
        o.Visible = False
        o.Exists = True

        NestBlock = 0
        For i = GetDefineBlock("game").StartLine + 1 To GetDefineBlock("game").EndLine - 1
            If NestBlock = 0 Then
                If BeginsWith(_lines(i), "define ") Then
                    NestBlock = NestBlock + 1
                ElseIf BeginsWith(_lines(i), "properties ") Then
                    AddToObjectProperties(RetrieveParameter(_lines(i), _nullContext), _numberObjs, _nullContext)
                ElseIf BeginsWith(_lines(i), "type ") Then
                    o.NumberTypesIncluded = o.NumberTypesIncluded + 1
                    ReDim Preserve o.TypesIncluded(o.NumberTypesIncluded)
                    o.TypesIncluded(o.NumberTypesIncluded) = RetrieveParameter(_lines(i), _nullContext)

                    PropertyData = GetPropertiesInType(RetrieveParameter(_lines(i), _nullContext))
                    AddToObjectProperties(PropertyData.Properties, _numberObjs, _nullContext)
                    For k = 1 To PropertyData.NumberActions
                        AddObjectAction(_numberObjs, PropertyData.Actions(k).ActionName, PropertyData.Actions(k).Script)
                    Next k
                ElseIf BeginsWith(_lines(i), "action ") Then
                    AddToObjectActions(GetEverythingAfter(_lines(i), "action "), _numberObjs, _nullContext)
                End If
            Else
                If Trim(_lines(i)) = "end define" Then
                    NestBlock = NestBlock - 1
                End If
            End If
        Next i

    End Sub

    Private Sub SetUpMenus()
        Dim j, i, SCP As Integer
        Dim MenuExists As Boolean = False

        Dim menuTitle As String = ""
        Dim menuOptions As New Dictionary(Of String, String)

        For i = 1 To _numberSections
            If BeginsWith(_lines(_defineBlocks(i).StartLine), "define menu ") Then

                If MenuExists Then
                    LogASLError("Can't load menu '" & RetrieveParameter(_lines(_defineBlocks(i).StartLine), _nullContext) & "' - only one menu can be added.", LogType.WarningError)
                Else
                    menuTitle = RetrieveParameter(_lines(_defineBlocks(i).StartLine), _nullContext)

                    For j = _defineBlocks(i).StartLine + 1 To _defineBlocks(i).EndLine - 1
                        If Trim(_lines(j)) <> "" Then
                            SCP = InStr(_lines(j), ":")
                            If SCP = 0 And _lines(j) <> "-" Then
                                LogASLError("No menu command specified in menu '" & menuTitle & "', item '" & _lines(j), LogType.WarningError)
                            Else
                                If _lines(j) = "-" Then
                                    menuOptions.Add("k" & CStr(j), "-")
                                Else
                                    menuOptions.Add(Trim(Mid(_lines(j), SCP + 1)), Trim(Left(_lines(j), SCP - 1)))
                                End If

                            End If
                        End If
                    Next j

                    If menuOptions.Count > 0 Then
                        MenuExists = True
                    End If
                End If
            End If
        Next i

        If MenuExists Then
            Dim windowMenu As New MenuData(menuTitle, menuOptions, False)
            m_player.SetWindowMenu(windowMenu)
        End If
    End Sub

    Private Sub SetUpOptions()
        Dim i As Integer
        Dim CurOpt As String

        For i = GetDefineBlock("options").StartLine + 1 To GetDefineBlock("options").EndLine - 1

            If BeginsWith(_lines(i), "panes ") Then
                CurOpt = LCase(Trim(GetEverythingAfter(_lines(i), "panes ")))
                m_player.SetPanesVisible(CurOpt)
            ElseIf BeginsWith(_lines(i), "abbreviations ") Then
                CurOpt = LCase(Trim(GetEverythingAfter(_lines(i), "abbreviations ")))
                If CurOpt = "off" Then _useAbbreviations = False Else _useAbbreviations = True
            End If
        Next i
    End Sub

    Private Sub SetUpRoomData()
        ' Sets up room data
        Dim NestedBlock As Integer
        Dim PropertyData As PropertiesActions
        Dim DefaultProperties As New PropertiesActions
        Dim DefaultExists As Boolean
        Dim k, i, j As Integer

        ' see if define type <defaultroom> exists:
        DefaultExists = False
        For i = 1 To _numberSections
            If Trim(_lines(_defineBlocks(i).StartLine)) = "define type <defaultroom>" Then
                DefaultExists = True
                DefaultProperties = GetPropertiesInType("defaultroom")
                i = _numberSections
            End If
        Next i

        Dim PlaceData As String
        Dim SCP As Integer
        For i = 1 To _numberSections
            If BeginsWith(_lines(_defineBlocks(i).StartLine), "define room ") Then
                _numberRooms = _numberRooms + 1
                ReDim Preserve _rooms(_numberRooms)
                _rooms(_numberRooms) = New RoomType

                _numberObjs = _numberObjs + 1
                ReDim Preserve _objs(_numberObjs)
                _objs(_numberObjs) = New ObjectType

                Dim r = _rooms(_numberRooms)

                r.RoomName = RetrieveParameter(_lines(_defineBlocks(i).StartLine), _nullContext)
                _objs(_numberObjs).ObjectName = r.RoomName
                _objs(_numberObjs).IsRoom = True
                _objs(_numberObjs).CorresRoom = r.RoomName
                _objs(_numberObjs).CorresRoomId = _numberRooms

                r.ObjID = _numberObjs

                If _gameAslVersion >= 410 Then
                    r.Exits = New RoomExits(Me)
                    r.Exits.ObjID = r.ObjID
                End If

                ' *******************************************************************************
                ' IF FURTHER CHANGES ARE MADE HERE, A NEW CREATEROOM SUB SHOULD BE CREATED, WHICH
                ' WE CAN THEN CALL FROM EXECUTECREATE ALSO.
                ' *******************************************************************************

                NestedBlock = 0

                If DefaultExists Then
                    AddToObjectProperties(DefaultProperties.Properties, _numberObjs, _nullContext)
                    For k = 1 To DefaultProperties.NumberActions
                        AddObjectAction(_numberObjs, DefaultProperties.Actions(k).ActionName, DefaultProperties.Actions(k).Script)
                    Next k
                End If

                For j = _defineBlocks(i).StartLine + 1 To _defineBlocks(i).EndLine - 1
                    If BeginsWith(_lines(j), "define ") Then
                        'skip nested blocks
                        NestedBlock = 1
                        Do
                            j = j + 1
                            If BeginsWith(_lines(j), "define ") Then
                                NestedBlock = NestedBlock + 1
                            ElseIf Trim(_lines(j)) = "end define" Then
                                NestedBlock = NestedBlock - 1
                            End If
                        Loop Until NestedBlock = 0
                    End If

                    If _gameAslVersion >= 280 And BeginsWith(_lines(j), "alias ") Then
                        r.RoomAlias = RetrieveParameter(_lines(j), _nullContext)
                        _objs(_numberObjs).ObjectAlias = r.RoomAlias
                        If _gameAslVersion >= 350 Then AddToObjectProperties("alias=" & r.RoomAlias, _numberObjs, _nullContext)
                    ElseIf _gameAslVersion >= 280 And BeginsWith(_lines(j), "description ") Then
                        r.Description = GetTextOrScript(GetEverythingAfter(_lines(j), "description "))
                        If _gameAslVersion >= 350 Then
                            If r.Description.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "description", r.Description.Data)
                            Else
                                AddToObjectProperties("description=" & r.Description.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "out ") Then
                        r.Out.Text = RetrieveParameter(_lines(j), _nullContext)
                        r.Out.Script = Trim(Mid(_lines(j), InStr(_lines(j), ">") + 1))
                        If _gameAslVersion >= 350 Then
                            If r.Out.Script <> "" Then
                                AddObjectAction(_numberObjs, "out", r.Out.Script)
                            End If

                            AddToObjectProperties("out=" & r.Out.Text, _numberObjs, _nullContext)
                        End If
                    ElseIf BeginsWith(_lines(j), "east ") Then
                        r.East = GetTextOrScript(GetEverythingAfter(_lines(j), "east "))
                        If _gameAslVersion >= 350 Then
                            If r.East.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "east", r.East.Data)
                            Else
                                AddToObjectProperties("east=" & r.East.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "west ") Then
                        r.West = GetTextOrScript(GetEverythingAfter(_lines(j), "west "))
                        If _gameAslVersion >= 350 Then
                            If r.West.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "west", r.West.Data)
                            Else
                                AddToObjectProperties("west=" & r.West.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "north ") Then
                        r.North = GetTextOrScript(GetEverythingAfter(_lines(j), "north "))
                        If _gameAslVersion >= 350 Then
                            If r.North.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "north", r.North.Data)
                            Else
                                AddToObjectProperties("north=" & r.North.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "south ") Then
                        r.South = GetTextOrScript(GetEverythingAfter(_lines(j), "south "))
                        If _gameAslVersion >= 350 Then
                            If r.South.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "south", r.South.Data)
                            Else
                                AddToObjectProperties("south=" & r.South.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "northeast ") Then
                        r.NorthEast = GetTextOrScript(GetEverythingAfter(_lines(j), "northeast "))
                        If _gameAslVersion >= 350 Then
                            If r.NorthEast.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "northeast", r.NorthEast.Data)
                            Else
                                AddToObjectProperties("northeast=" & r.NorthEast.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "northwest ") Then
                        r.NorthWest = GetTextOrScript(GetEverythingAfter(_lines(j), "northwest "))
                        If _gameAslVersion >= 350 Then
                            If r.NorthWest.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "northwest", r.NorthWest.Data)
                            Else
                                AddToObjectProperties("northwest=" & r.NorthWest.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "southeast ") Then
                        r.SouthEast = GetTextOrScript(GetEverythingAfter(_lines(j), "southeast "))
                        If _gameAslVersion >= 350 Then
                            If r.SouthEast.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "southeast", r.SouthEast.Data)
                            Else
                                AddToObjectProperties("southeast=" & r.SouthEast.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "southwest ") Then
                        r.SouthWest = GetTextOrScript(GetEverythingAfter(_lines(j), "southwest "))
                        If _gameAslVersion >= 350 Then
                            If r.SouthWest.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "southwest", r.SouthWest.Data)
                            Else
                                AddToObjectProperties("southwest=" & r.SouthWest.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "up ") Then
                        r.Up = GetTextOrScript(GetEverythingAfter(_lines(j), "up "))
                        If _gameAslVersion >= 350 Then
                            If r.Up.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "up", r.Up.Data)
                            Else
                                AddToObjectProperties("up=" & r.Up.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf BeginsWith(_lines(j), "down ") Then
                        r.Down = GetTextOrScript(GetEverythingAfter(_lines(j), "down "))
                        If _gameAslVersion >= 350 Then
                            If r.Down.Type = TextActionType.Script Then
                                AddObjectAction(_numberObjs, "down", r.Down.Data)
                            Else
                                AddToObjectProperties("down=" & r.Down.Data, _numberObjs, _nullContext)
                            End If
                        End If
                    ElseIf _gameAslVersion >= 280 And BeginsWith(_lines(j), "indescription ") Then
                        r.InDescription = RetrieveParameter(_lines(j), _nullContext)
                        If _gameAslVersion >= 350 Then AddToObjectProperties("indescription=" & r.InDescription, _numberObjs, _nullContext)
                    ElseIf _gameAslVersion >= 280 And BeginsWith(_lines(j), "look ") Then
                        r.Look = RetrieveParameter(_lines(j), _nullContext)
                        If _gameAslVersion >= 350 Then AddToObjectProperties("look=" & r.Look, _numberObjs, _nullContext)
                    ElseIf BeginsWith(_lines(j), "prefix ") Then
                        r.Prefix = RetrieveParameter(_lines(j), _nullContext)
                        If _gameAslVersion >= 350 Then AddToObjectProperties("prefix=" & r.Prefix, _numberObjs, _nullContext)
                    ElseIf BeginsWith(_lines(j), "script ") Then
                        r.Script = GetEverythingAfter(_lines(j), "script ")
                        AddObjectAction(_numberObjs, "script", r.Script)
                    ElseIf BeginsWith(_lines(j), "command ") Then
                        r.NumberCommands = r.NumberCommands + 1
                        ReDim Preserve r.Commands(r.NumberCommands)
                        r.Commands(r.NumberCommands) = New UserDefinedCommandType
                        r.Commands(r.NumberCommands).CommandText = RetrieveParameter(_lines(j), _nullContext, False)
                        r.Commands(r.NumberCommands).CommandScript = Trim(Mid(_lines(j), InStr(_lines(j), ">") + 1))
                    ElseIf BeginsWith(_lines(j), "place ") Then
                        r.NumberPlaces = r.NumberPlaces + 1
                        ReDim Preserve r.Places(r.NumberPlaces)
                        r.Places(r.NumberPlaces) = New PlaceType
                        PlaceData = RetrieveParameter(_lines(j), _nullContext)
                        SCP = InStr(PlaceData, ";")
                        If SCP = 0 Then
                            r.Places(r.NumberPlaces).PlaceName = PlaceData
                        Else
                            r.Places(r.NumberPlaces).PlaceName = Trim(Mid(PlaceData, SCP + 1))
                            r.Places(r.NumberPlaces).Prefix = Trim(Left(PlaceData, SCP - 1))
                        End If
                        r.Places(r.NumberPlaces).Script = Trim(Mid(_lines(j), InStr(_lines(j), ">") + 1))
                    ElseIf BeginsWith(_lines(j), "use ") Then
                        r.NumberUse = r.NumberUse + 1
                        ReDim Preserve r.Use(r.NumberUse)
                        r.Use(r.NumberUse) = New ScriptText
                        r.Use(r.NumberUse).Text = RetrieveParameter(_lines(j), _nullContext)
                        r.Use(r.NumberUse).Script = Trim(Mid(_lines(j), InStr(_lines(j), ">") + 1))
                    ElseIf BeginsWith(_lines(j), "properties ") Then
                        AddToObjectProperties(RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                    ElseIf BeginsWith(_lines(j), "type ") Then
                        _objs(_numberObjs).NumberTypesIncluded = _objs(_numberObjs).NumberTypesIncluded + 1
                        ReDim Preserve _objs(_numberObjs).TypesIncluded(_objs(_numberObjs).NumberTypesIncluded)
                        _objs(_numberObjs).TypesIncluded(_objs(_numberObjs).NumberTypesIncluded) = RetrieveParameter(_lines(j), _nullContext)

                        PropertyData = GetPropertiesInType(RetrieveParameter(_lines(j), _nullContext))
                        AddToObjectProperties(PropertyData.Properties, _numberObjs, _nullContext)
                        For k = 1 To PropertyData.NumberActions
                            AddObjectAction(_numberObjs, PropertyData.Actions(k).ActionName, PropertyData.Actions(k).Script)
                        Next k
                    ElseIf BeginsWith(_lines(j), "action ") Then
                        AddToObjectActions(GetEverythingAfter(_lines(j), "action "), _numberObjs, _nullContext)
                    ElseIf BeginsWith(_lines(j), "beforeturn ") Then
                        r.BeforeTurnScript = r.BeforeTurnScript & GetEverythingAfter(_lines(j), "beforeturn ") & vbCrLf
                    ElseIf BeginsWith(_lines(j), "afterturn ") Then
                        r.AfterTurnScript = r.AfterTurnScript & GetEverythingAfter(_lines(j), "afterturn ") & vbCrLf
                    End If
                Next j
            End If
        Next i
    End Sub

    Private Sub SetUpSynonyms()
        ' Sets up synonyms when game is initialised

        Dim SynonymBlock As DefineBlock
        Dim EqualsSignPos As Integer
        Dim OriginalWordsList, ConvertWord As String
        Dim i, iCurPos, EndOfWord As Integer
        Dim ThisWord As String

        SynonymBlock = GetDefineBlock("synonyms")

        _numberSynonyms = 0

        If SynonymBlock.StartLine = 0 And SynonymBlock.EndLine = 0 Then
            Exit Sub
        End If

        For i = SynonymBlock.StartLine + 1 To SynonymBlock.EndLine - 1
            EqualsSignPos = InStr(_lines(i), "=")
            If EqualsSignPos <> 0 Then
                OriginalWordsList = Trim(Left(_lines(i), EqualsSignPos - 1))
                ConvertWord = Trim(Mid(_lines(i), EqualsSignPos + 1))

                'Go through each word in OriginalWordsList (sep.
                'by ";"):

                OriginalWordsList = OriginalWordsList & ";"
                iCurPos = 1

                Do
                    EndOfWord = InStr(iCurPos, OriginalWordsList, ";")
                    ThisWord = Trim(Mid(OriginalWordsList, iCurPos, EndOfWord - iCurPos))

                    If InStr(" " & ConvertWord & " ", " " & ThisWord & " ") > 0 Then
                        ' Recursive synonym
                        LogASLError("Recursive synonym detected: '" & ThisWord & "' converting to '" & ConvertWord & "'", LogType.WarningError)
                    Else
                        _numberSynonyms = _numberSynonyms + 1
                        ReDim Preserve _synonyms(_numberSynonyms)
                        _synonyms(_numberSynonyms) = New SynonymType
                        _synonyms(_numberSynonyms).OriginalWord = ThisWord
                        _synonyms(_numberSynonyms).ConvertTo = ConvertWord
                    End If
                    iCurPos = EndOfWord + 1
                Loop Until iCurPos >= Len(OriginalWordsList)
            End If

        Next i

    End Sub

    Private Sub SetUpTimers()
        Dim i, j As Integer

        For i = 1 To _numberSections
            If BeginsWith(_lines(_defineBlocks(i).StartLine), "define timer ") Then
                _numberTimers = _numberTimers + 1
                ReDim Preserve _timers(_numberTimers)
                _timers(_numberTimers) = New TimerType
                _timers(_numberTimers).TimerName = RetrieveParameter(_lines(_defineBlocks(i).StartLine), _nullContext)
                _timers(_numberTimers).TimerActive = False

                For j = _defineBlocks(i).StartLine + 1 To _defineBlocks(i).EndLine - 1
                    If BeginsWith(_lines(j), "interval ") Then
                        _timers(_numberTimers).TimerInterval = CInt(RetrieveParameter(_lines(j), _nullContext))
                    ElseIf BeginsWith(_lines(j), "action ") Then
                        _timers(_numberTimers).TimerAction = GetEverythingAfter(_lines(j), "action ")
                    ElseIf Trim(LCase(_lines(j))) = "enabled" Then
                        _timers(_numberTimers).TimerActive = True
                    ElseIf Trim(LCase(_lines(j))) = "disabled" Then
                        _timers(_numberTimers).TimerActive = False
                    End If
                Next j
            End If
        Next i

    End Sub

    Private Sub SetUpTurnScript()
        Dim gameblock As DefineBlock
        Dim i As Integer
        gameblock = GetDefineBlock("game")

        _beforeTurnScript = ""
        _afterTurnScript = ""

        For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
            If BeginsWith(_lines(i), "beforeturn ") Then
                _beforeTurnScript = _beforeTurnScript & GetEverythingAfter(Trim(_lines(i)), "beforeturn ") & vbCrLf
            ElseIf BeginsWith(_lines(i), "afterturn ") Then
                _afterTurnScript = _afterTurnScript & GetEverythingAfter(Trim(_lines(i)), "afterturn ") & vbCrLf
            End If
        Next i
    End Sub

    Private Sub SetUpUserDefinedPlayerErrors()
        ' goes through "define game" block and sets stored error
        ' messages accordingly

        Dim gameblock As DefineBlock
        Dim sErrorName, sErrorMsg As String
        Dim iCurrentError As PlayerError, i As Integer
        Dim ExamineIsCustomised As Boolean
        Dim ErrorInfo As String
        Dim SemiColonPos As Integer

        gameblock = GetDefineBlock("game")

        ExamineIsCustomised = False

        For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
            If BeginsWith(_lines(i), "error ") Then
                ErrorInfo = RetrieveParameter(_lines(i), _nullContext, False)
                SemiColonPos = InStr(ErrorInfo, ";")

                sErrorName = Left(ErrorInfo, SemiColonPos - 1)
                sErrorMsg = Trim(Mid(ErrorInfo, SemiColonPos + 1))

                iCurrentError = 0
                Select Case sErrorName
                    Case "badcommand"
                        iCurrentError = PlayerError.BadCommand
                    Case "badgo"
                        iCurrentError = PlayerError.BadGo
                    Case "badgive"
                        iCurrentError = PlayerError.BadGive
                    Case "badcharacter"
                        iCurrentError = PlayerError.BadCharacter
                    Case "noitem"
                        iCurrentError = PlayerError.NoItem
                    Case "itemunwanted"
                        iCurrentError = PlayerError.ItemUnwanted
                    Case "badlook"
                        iCurrentError = PlayerError.BadLook
                    Case "badthing"
                        iCurrentError = PlayerError.BadThing
                    Case "defaultlook"
                        iCurrentError = PlayerError.DefaultLook
                    Case "defaultspeak"
                        iCurrentError = PlayerError.DefaultSpeak
                    Case "baditem"
                        iCurrentError = PlayerError.BadItem
                    Case "baddrop"
                        iCurrentError = PlayerError.BadDrop
                    Case "defaultake"
                        If _gameAslVersion <= 280 Then
                            iCurrentError = PlayerError.BadTake
                        Else
                            iCurrentError = PlayerError.DefaultTake
                        End If
                    Case "baduse"
                        iCurrentError = PlayerError.BadUse
                    Case "defaultuse"
                        iCurrentError = PlayerError.DefaultUse
                    Case "defaultout"
                        iCurrentError = PlayerError.DefaultOut
                    Case "badplace"
                        iCurrentError = PlayerError.BadPlace
                    Case "badexamine"
                        If _gameAslVersion >= 310 Then
                            iCurrentError = PlayerError.BadExamine
                        End If
                    Case "defaultexamine"
                        iCurrentError = PlayerError.DefaultExamine
                        ExamineIsCustomised = True
                    Case "badtake"
                        iCurrentError = PlayerError.BadTake
                    Case "cantdrop"
                        iCurrentError = PlayerError.CantDrop
                    Case "defaultdrop"
                        iCurrentError = PlayerError.DefaultDrop
                    Case "badpronoun"
                        iCurrentError = PlayerError.BadPronoun
                    Case "alreadyopen"
                        iCurrentError = PlayerError.AlreadyOpen
                    Case "alreadyclosed"
                        iCurrentError = PlayerError.AlreadyClosed
                    Case "cantopen"
                        iCurrentError = PlayerError.CantOpen
                    Case "cantclose"
                        iCurrentError = PlayerError.CantClose
                    Case "defaultopen"
                        iCurrentError = PlayerError.DefaultOpen
                    Case "defaultclose"
                        iCurrentError = PlayerError.DefaultClose
                    Case "badput"
                        iCurrentError = PlayerError.BadPut
                    Case "cantput"
                        iCurrentError = PlayerError.CantPut
                    Case "defaultput"
                        iCurrentError = PlayerError.DefaultPut
                    Case "cantremove"
                        iCurrentError = PlayerError.CantRemove
                    Case "alreadyput"
                        iCurrentError = PlayerError.AlreadyPut
                    Case "defaultremove"
                        iCurrentError = PlayerError.DefaultRemove
                    Case "locked"
                        iCurrentError = PlayerError.Locked
                    Case "defaultwait"
                        iCurrentError = PlayerError.DefaultWait
                    Case "alreadytaken"
                        iCurrentError = PlayerError.AlreadyTaken
                End Select

                _playerErrorMessageString(iCurrentError) = sErrorMsg
                If iCurrentError = PlayerError.DefaultLook And Not ExamineIsCustomised Then
                    ' If we're setting the default look message, and we've not already customised the
                    ' default examine message, then set the default examine message to the same thing.
                    _playerErrorMessageString(PlayerError.DefaultExamine) = sErrorMsg
                End If
            End If
        Next i

    End Sub

    Private Sub SetVisibility(ThingString As String, ThingType As Thing, ThingVisible As Boolean, ctx As Context)
        ' Sets visibilty of objects and characters

        Dim i, AtPos As Integer
        Dim CRoom, CName As String

        Dim FoundObject As Boolean
        If _gameAslVersion >= 280 Then
            FoundObject = False

            For i = 1 To _numberObjs
                If LCase(_objs(i).ObjectName) = LCase(ThingString) Then
                    _objs(i).Visible = ThingVisible
                    If ThingVisible Then
                        AddToObjectProperties("not invisible", i, ctx)
                    Else
                        AddToObjectProperties("invisible", i, ctx)
                    End If

                    i = _numberObjs + 1
                    FoundObject = True
                End If
            Next i

            If Not FoundObject Then
                LogASLError("Not found object '" & ThingString & "'", LogType.WarningError)
            End If
        Else
            ' split ThingString into character name and room
            ' (thingstring of form name@room)

            AtPos = InStr(ThingString, "@")

            ' If no room specified, current room presumed
            If AtPos = 0 Then
                CRoom = _currentRoom
                CName = ThingString
            Else
                CName = Trim(Left(ThingString, AtPos - 1))
                CRoom = Trim(Right(ThingString, Len(ThingString) - AtPos))
            End If

            If ThingType = Thing.Character Then
                For i = 1 To _numberChars
                    If LCase(_chars(i).ContainerRoom) = LCase(CRoom) And LCase(_chars(i).ObjectName) = LCase(CName) Then
                        _chars(i).Visible = ThingVisible
                        i = _numberChars + 1
                    End If
                Next i
            ElseIf ThingType = Thing.Object Then
                For i = 1 To _numberObjs
                    If LCase(_objs(i).ContainerRoom) = LCase(CRoom) And LCase(_objs(i).ObjectName) = LCase(CName) Then
                        _objs(i).Visible = ThingVisible
                        i = _numberObjs + 1
                    End If
                Next i
            End If
        End If

        UpdateObjectList(ctx)
    End Sub

    Private Sub ShowPictureInText(sFileName As String)
        If Not m_useStaticFrameForPictures Then
            m_player.ShowPicture(sFileName)
        Else
            ' Workaround for a particular game which expects pictures to be in a popup window -
            ' use the static picture frame feature so that image is not cleared
            m_player.SetPanelContents("<img src=""" + m_player.GetURL(sFileName) + """ onload=""setPanelHeight()""/>")
        End If
    End Sub

    Private Sub ShowRoomInfoV2(Room As String)
        ' ShowRoomInfo for Quest 2.x games
        Dim i As Integer

        Dim RoomDisplayText As String = ""
        Dim DescTagExist As Boolean
        Dim gameblock As DefineBlock
        Dim CharsViewable As String
        Dim CharsFound As Integer
        Dim PANF, Prefix, PA, InDesc As String
        Dim AliasName As String = ""
        Dim CharList As String
        Dim FoundLastComma, CommaPos, NewCommaPos As Integer
        Dim NoFormatObjsViewable As String
        Dim ObjsViewable As String = ""
        Dim ObjsFound As Integer
        Dim ObjListString, NFObjListString As String
        Dim PossDir, NSEW, Doorways, Places, PL As String
        Dim AliasOut As String = ""
        Dim PLNF As String
        Dim DescLine As String = ""
        Dim LastComma, OldLastComma As Integer
        Dim DefBlk As Integer
        Dim LookString As String = ""

        gameblock = GetDefineBlock("game")
        _currentRoom = Room

        'find the room
        Dim roomblock As DefineBlock
        roomblock = DefineBlockParam("room", Room)
        Dim FinishedFindingCommas As Boolean

        CharsViewable = ""
        CharsFound = 0

        'see if room has an alias
        For i = roomblock.StartLine + 1 To roomblock.EndLine - 1
            If BeginsWith(_lines(i), "alias") Then
                AliasName = RetrieveParameter(_lines(i), _nullContext)
                i = roomblock.EndLine
            End If
        Next i
        If AliasName = "" Then AliasName = Room

        'see if room has a prefix
        Prefix = FindStatement(roomblock, "prefix")
        If Prefix = "" Then
            PA = "|cr" & AliasName & "|cb"
            PANF = AliasName ' No formatting version, for label
        Else
            PA = Prefix & " |cr" & AliasName & "|cb"
            PANF = Prefix & " " & AliasName
        End If

        'print player's location
        'find indescription line:
        InDesc = "unfound"
        For i = roomblock.StartLine + 1 To roomblock.EndLine - 1
            If BeginsWith(_lines(i), "indescription") Then
                InDesc = Trim(RetrieveParameter(_lines(i), _nullContext))
                i = roomblock.EndLine
            End If
        Next i

        If InDesc <> "unfound" Then
            ' Print player's location according to indescription:
            If Right(InDesc, 1) = ":" Then
                ' if line ends with a colon, add place name:
                RoomDisplayText = RoomDisplayText & Left(InDesc, Len(InDesc) - 1) & " " & PA & "." & vbCrLf
            Else
                ' otherwise, just print the indescription line:
                RoomDisplayText = RoomDisplayText & InDesc & vbCrLf
            End If
        Else
            ' if no indescription line, print the default.
            RoomDisplayText = RoomDisplayText & "You are in " & PA & "." & vbCrLf
        End If

        m_player.LocationUpdated(PANF)

        SetStringContents("quest.formatroom", PANF, _nullContext)

        'FIND CHARACTERS ===

        ' go through Chars() array
        For i = 1 To _numberChars
            If _chars(i).ContainerRoom = Room And _chars(i).Exists And _chars(i).Visible Then
                CharsViewable = CharsViewable & _chars(i).Prefix & "|b" & _chars(i).ObjectName & "|xb" & _chars(i).Suffix & ", "
                CharsFound = CharsFound + 1
            End If
        Next i

        If CharsFound = 0 Then
            CharsViewable = "There is nobody here."
            SetStringContents("quest.characters", "", _nullContext)
        Else
            'chop off final comma and add full stop (.)
            CharList = Left(CharsViewable, Len(CharsViewable) - 2)
            SetStringContents("quest.characters", CharList, _nullContext)

            'if more than one character, add "and" before
            'last one:
            CommaPos = InStr(CharList, ",")
            If CommaPos <> 0 Then
                FoundLastComma = 0
                Do
                    NewCommaPos = InStr(CommaPos + 1, CharList, ",")
                    If NewCommaPos = 0 Then
                        FoundLastComma = 1
                    Else
                        CommaPos = NewCommaPos
                    End If
                Loop Until FoundLastComma = 1

                CharList = Trim(Left(CharList, CommaPos - 1)) & " and " & Trim(Mid(CharList, CommaPos + 1))
            End If

            CharsViewable = "You can see " & CharList & " here."
        End If

        RoomDisplayText = RoomDisplayText & CharsViewable & vbCrLf

        'FIND OBJECTS

        NoFormatObjsViewable = ""

        For i = 1 To _numberObjs
            If _objs(i).ContainerRoom = Room And _objs(i).Exists And _objs(i).Visible Then
                ObjsViewable = ObjsViewable & _objs(i).Prefix & "|b" & _objs(i).ObjectName & "|xb" & _objs(i).Suffix & ", "
                NoFormatObjsViewable = NoFormatObjsViewable & _objs(i).Prefix & _objs(i).ObjectName & ", "

                ObjsFound = ObjsFound + 1
            End If
        Next i

        Dim bFinLoop As Boolean
        If ObjsFound <> 0 Then
            ObjListString = Left(ObjsViewable, Len(ObjsViewable) - 2)
            NFObjListString = Left(NoFormatObjsViewable, Len(NoFormatObjsViewable) - 2)

            CommaPos = InStr(ObjListString, ",")
            If CommaPos <> 0 Then
                Do
                    NewCommaPos = InStr(CommaPos + 1, ObjListString, ",")
                    If NewCommaPos = 0 Then
                        bFinLoop = True
                    Else
                        CommaPos = NewCommaPos
                    End If
                Loop Until bFinLoop

                ObjListString = Trim(Left(ObjListString, CommaPos - 1) & " and " & Trim(Mid(ObjListString, CommaPos + 1)))
            End If

            ObjsViewable = "There is " & ObjListString & " here."
            SetStringContents("quest.objects", Left(NoFormatObjsViewable, Len(NoFormatObjsViewable) - 2), _nullContext)
            SetStringContents("quest.formatobjects", ObjListString, _nullContext)
            RoomDisplayText = RoomDisplayText & ObjsViewable & vbCrLf
        Else
            SetStringContents("quest.objects", "", _nullContext)
            SetStringContents("quest.formatobjects", "", _nullContext)
        End If

        'FIND DOORWAYS
        Doorways = ""
        NSEW = ""
        Places = ""
        PossDir = ""

        For i = roomblock.StartLine + 1 To roomblock.EndLine - 1
            If BeginsWith(_lines(i), "out") Then
                Doorways = RetrieveParameter(_lines(i), _nullContext)
            End If

            If BeginsWith(_lines(i), "north ") Then
                NSEW = NSEW & "|bnorth|xb, "
                PossDir = PossDir & "n"
            ElseIf BeginsWith(_lines(i), "south ") Then
                NSEW = NSEW & "|bsouth|xb, "
                PossDir = PossDir & "s"
            ElseIf BeginsWith(_lines(i), "east ") Then
                NSEW = NSEW & "|beast|xb, "
                PossDir = PossDir & "e"
            ElseIf BeginsWith(_lines(i), "west ") Then
                NSEW = NSEW & "|bwest|xb, "
                PossDir = PossDir & "w"
            ElseIf BeginsWith(_lines(i), "northeast ") Then
                NSEW = NSEW & "|bnortheast|xb, "
                PossDir = PossDir & "a"
            ElseIf BeginsWith(_lines(i), "northwest ") Then
                NSEW = NSEW & "|bnorthwest|xb, "
                PossDir = PossDir & "b"
            ElseIf BeginsWith(_lines(i), "southeast ") Then
                NSEW = NSEW & "|bsoutheast|xb, "
                PossDir = PossDir & "c"
            ElseIf BeginsWith(_lines(i), "southwest ") Then
                NSEW = NSEW & "|bsouthwest|xb, "
                PossDir = PossDir & "d"
            End If

            If BeginsWith(_lines(i), "place") Then
                'remove any prefix semicolon from printed text
                PL = RetrieveParameter(_lines(i), _nullContext)
                PLNF = PL 'Used in object list - no formatting or prefix
                If InStr(PL, ";") > 0 Then
                    PLNF = Right(PL, Len(PL) - (InStr(PL, ";") + 1))
                    PL = Trim(Left(PL, InStr(PL, ";") - 1)) & " |b" & Right(PL, Len(PL) - (InStr(PL, ";") + 1)) & "|xb"
                Else
                    PL = "|b" & PL & "|xb"
                End If
                Places = Places & PL & ", "

            End If

        Next i

        Dim outside As DefineBlock
        If Doorways <> "" Then
            'see if outside has an alias
            outside = DefineBlockParam("room", Doorways)
            For i = outside.StartLine + 1 To outside.EndLine - 1
                If BeginsWith(_lines(i), "alias") Then
                    AliasOut = RetrieveParameter(_lines(i), _nullContext)
                    i = outside.EndLine
                End If
            Next i
            If AliasOut = "" Then AliasOut = Doorways

            RoomDisplayText = RoomDisplayText & "You can go out to " & AliasOut & "." & vbCrLf
            PossDir = PossDir & "o"
            SetStringContents("quest.doorways.out", AliasOut, _nullContext)
        Else
            SetStringContents("quest.doorways.out", "", _nullContext)
        End If

        Dim bFinNSEW As Boolean
        If NSEW <> "" Then
            'strip final comma
            NSEW = Left(NSEW, Len(NSEW) - 2)
            CommaPos = InStr(NSEW, ",")
            If CommaPos <> 0 Then
                bFinNSEW = False
                Do
                    NewCommaPos = InStr(CommaPos + 1, NSEW, ",")
                    If NewCommaPos = 0 Then
                        bFinNSEW = True
                    Else
                        CommaPos = NewCommaPos
                    End If
                Loop Until bFinNSEW

                NSEW = Trim(Left(NSEW, CommaPos - 1)) & " or " & Trim(Mid(NSEW, CommaPos + 1))
            End If

            RoomDisplayText = RoomDisplayText & "You can go " & NSEW & "." & vbCrLf
            SetStringContents("quest.doorways.dirs", NSEW, _nullContext)
        Else
            SetStringContents("quest.doorways.dirs", "", _nullContext)
        End If

        UpdateDirButtons(PossDir, _nullContext)

        If Places <> "" Then
            'strip final comma
            Places = Left(Places, Len(Places) - 2)

            'if there is still a comma here, there is more than
            'one place, so add the word "or" before the last one.
            If InStr(Places, ",") > 0 Then
                LastComma = 0
                FinishedFindingCommas = False
                Do
                    OldLastComma = LastComma
                    LastComma = InStr(LastComma + 1, Places, ",")
                    If LastComma = 0 Then
                        FinishedFindingCommas = True
                        LastComma = OldLastComma
                    End If
                Loop Until FinishedFindingCommas

                Places = Left(Places, LastComma) & " or" & Right(Places, Len(Places) - LastComma)
            End If

            RoomDisplayText = RoomDisplayText & "You can go to " & Places & "." & vbCrLf
            SetStringContents("quest.doorways.places", Places, _nullContext)
        Else
            SetStringContents("quest.doorways.places", "", _nullContext)
        End If

        'Print RoomDisplayText if there is no "description" tag,
        'otherwise execute the description tag information:

        ' First, look in the "define room" block:
        DescTagExist = False
        For i = roomblock.StartLine + 1 To roomblock.EndLine - 1
            If BeginsWith(_lines(i), "description ") Then
                DescLine = _lines(i)
                DescTagExist = True
                i = roomblock.EndLine
            End If
        Next i

        If DescTagExist = False Then
            'Look in the "define game" block:
            For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
                If BeginsWith(_lines(i), "description ") Then
                    DescLine = _lines(i)
                    DescTagExist = True
                    i = gameblock.EndLine
                End If
            Next i
        End If

        If DescTagExist = False Then
            'Remove final vbCrLf:
            RoomDisplayText = Left(RoomDisplayText, Len(RoomDisplayText) - 2)
            Print(RoomDisplayText, _nullContext)
        Else
            'execute description tag:
            'If no script, just print the tag's parameter.
            'Otherwise, execute it as ASL script:

            DescLine = GetEverythingAfter(Trim(DescLine), "description ")
            If Left(DescLine, 1) = "<" Then
                Print(RetrieveParameter(DescLine, _nullContext), _nullContext)
            Else
                ExecuteScript(DescLine, _nullContext)
            End If
        End If

        UpdateObjectList(_nullContext)

        DefBlk = 0

        For i = roomblock.StartLine + 1 To roomblock.EndLine - 1
            ' don't get the 'look' statements in nested define blocks
            If BeginsWith(_lines(i), "define") Then DefBlk = DefBlk + 1
            If BeginsWith(_lines(i), "end define") Then DefBlk = DefBlk - 1
            If BeginsWith(_lines(i), "look") And DefBlk = 0 Then
                LookString = RetrieveParameter(_lines(i), _nullContext)
                i = roomblock.EndLine
            End If
        Next i

        If LookString <> "" Then Print(LookString, _nullContext)

    End Sub

    Private Sub Speak(Text As String)
        m_player.Speak(Text)
    End Sub

    Private Sub AddToObjectList(objList As List(Of ListData), exitList As List(Of ListData), ObjName As String, ObjType As Thing)
        ObjName = CapFirst(ObjName)

        If ObjType = Thing.Room Then
            objList.Add(New ListData(ObjName, m_listVerbs(ListType.ExitsList)))
            exitList.Add(New ListData(ObjName, m_listVerbs(ListType.ExitsList)))
        Else
            ' TO DO: DisplayType is not supported, should it be...?
            objList.Add(New ListData(ObjName, m_listVerbs(ListType.ObjectsList)))
        End If

    End Sub

    Private Sub ExecExec(ScriptLine As String, ctx As Context)
        Dim EX, ExecLine, R As String
        Dim SemiColonPos As Integer

        If ctx.CancelExec Then Exit Sub

        ExecLine = RetrieveParameter(ScriptLine, ctx)

        Dim NewThread As Context = CopyContext(ctx)

        NewThread.StackCounter = NewThread.StackCounter + 1

        If NewThread.StackCounter > 500 Then
            LogASLError("Out of stack space running '" & ScriptLine & "' - infinite loop?", LogType.WarningError)
            ctx.CancelExec = True
            Exit Sub
        End If

        If _gameAslVersion >= 310 Then
            NewThread.AllowRealNamesInCommand = True
        End If

        If InStr(ExecLine, ";") = 0 Then
            Try
                ExecCommand(ExecLine, NewThread, False)
            Catch
                LogASLError("Internal error " & Err.Number & " running '" & ScriptLine & "'", LogType.WarningError)
                ctx.CancelExec = True
            End Try
        Else
            SemiColonPos = InStr(ExecLine, ";")
            EX = Trim(Left(ExecLine, SemiColonPos - 1))
            R = Trim(Mid(ExecLine, SemiColonPos + 1))
            If R = "normal" Then
                ExecCommand(EX, NewThread, False, False)
            Else
                LogASLError("Unrecognised post-command parameter in " & Trim(ScriptLine), LogType.WarningError)
            End If
        End If
    End Sub

    Private Sub ExecSetString(StringInfo As String, ctx As Context)
        ' Sets string contents from a script parameter.
        ' Eg <string1;contents> sets string variable string1
        ' to "contents"

        Dim iSemiColonPos As Integer
        Dim sVarName As String
        Dim sVarCont As String
        Dim ArrayIndex As Integer

        iSemiColonPos = InStr(StringInfo, ";")
        sVarName = Trim(Left(StringInfo, iSemiColonPos - 1))
        sVarCont = Mid(StringInfo, iSemiColonPos + 1)

        If IsNumeric(sVarName) Then
            LogASLError("Invalid string name '" & sVarName & "' - string names cannot be numeric", LogType.WarningError)
            Exit Sub
        End If

        If _gameAslVersion >= 281 Then
            sVarCont = Trim(sVarCont)
            If Left(sVarCont, 1) = "[" And Right(sVarCont, 1) = "]" Then
                sVarCont = Mid(sVarCont, 2, Len(sVarCont) - 2)
            End If
        End If

        ArrayIndex = GetArrayIndex(sVarName, ctx)

        SetStringContents(sVarName, sVarCont, ctx, ArrayIndex)

    End Sub

    Private Function ExecUserCommand(thecommand As String, ctx As Context, Optional LibCommands As Boolean = False) As Boolean
        'Executes a user-defined command. If unavailable, returns
        'false.
        Dim FoundCommand As Boolean
        Dim RoomID As Integer
        Dim roomblock As DefineBlock
        Dim CurCmd, CommandList As String
        Dim ScriptToExecute As String = ""
        Dim EndPos, i As Integer
        Dim CommandTag As String
        Dim CommandLine As String = ""
        Dim ScriptPos As Integer
        FoundCommand = False

        'First, check for a command in the current room block
        RoomID = GetRoomID(_currentRoom, ctx)

        ' RoomID is 0 if we have no rooms in the game. Unlikely, but we get an RTE otherwise.
        If RoomID <> 0 Then
            Dim r = _rooms(RoomID)
            For i = 1 To r.NumberCommands
                CommandList = r.Commands(i).CommandText
                Do
                    EndPos = InStr(CommandList, ";")
                    If EndPos = 0 Then
                        CurCmd = CommandList
                    Else
                        CurCmd = Trim(Left(CommandList, EndPos - 1))
                        CommandList = Trim(Mid(CommandList, EndPos + 1))
                    End If

                    If IsCompatible(LCase(thecommand), LCase(CurCmd)) Then
                        CommandLine = CurCmd
                        ScriptToExecute = r.Commands(i).CommandScript
                        FoundCommand = True
                        i = r.NumberCommands
                        Exit Do
                    End If
                Loop Until EndPos = 0
            Next i
        End If

        If Not LibCommands Then
            CommandTag = "command"
        Else
            CommandTag = "lib command"
        End If

        If Not FoundCommand Then
            ' Check "define game" block
            roomblock = GetDefineBlock("game")
            For i = roomblock.StartLine + 1 To roomblock.EndLine - 1
                If BeginsWith(_lines(i), CommandTag) Then

                    CommandList = RetrieveParameter(_lines(i), ctx, False)
                    Do
                        EndPos = InStr(CommandList, ";")
                        If EndPos = 0 Then
                            CurCmd = CommandList
                        Else
                            CurCmd = Trim(Left(CommandList, EndPos - 1))
                            CommandList = Trim(Mid(CommandList, EndPos + 1))
                        End If

                        If IsCompatible(LCase(thecommand), LCase(CurCmd)) Then
                            CommandLine = CurCmd
                            ScriptPos = InStr(_lines(i), ">") + 1
                            ScriptToExecute = Trim(Mid(_lines(i), ScriptPos))
                            FoundCommand = True
                            i = roomblock.EndLine
                            Exit Do
                        End If
                    Loop Until EndPos = 0
                End If
            Next i
        End If

        If FoundCommand Then
            If GetCommandParameters(thecommand, CommandLine, ctx) Then
                ExecuteScript(ScriptToExecute, ctx)
            End If
        End If

        Return FoundCommand
    End Function

    Private Sub ExecuteChoose(choicesection As String, ctx As Context)
        ExecuteScript(SetUpChoiceForm(choicesection, ctx), ctx)
    End Sub

    Private Function GetCommandParameters(TestLine As String, RequiredLine As String, ctx As Context) As Boolean
        'Gets parameters from line. For example, if RequiredLine
        'is "read #1#" and TestLine is "read sign", #1# returns
        '"sign".

        ' Returns FALSE if #@object# form used and object doesn't
        ' exist.

        Dim CurrentReqLinePos, NextVarPos As Integer
        Dim CheckChunk As String
        Dim CurrentTestLinePos As Integer
        Dim FinishedProcessing As Boolean
        Dim CurrentVariable As String = ""
        Dim ChunkBegin, ChunkEnd As Integer
        Dim ChunksBegin() As Integer
        Dim ChunksEnd() As Integer
        Dim NumberChunks As Integer
        Dim VarName() As String
        Dim Var2Pos, ArrayIndex As Integer
        Dim CurChunk As String
        Dim ObjID As Integer
        Dim success As Boolean
        Dim i As Integer

        ' Add dots before and after both strings. This fudge
        ' stops problems caused when variables are right at the
        ' beginning or end of a line.
        ' PostScript: well, it used to, I'm not sure if it's really
        ' required now though....
        ' As of Quest 4.0 we use the � character rather than a dot.
        TestLine = "�" & Trim(TestLine) & "�"
        RequiredLine = "�" & RequiredLine & "�"

        'Go through RequiredLine in chunks going up to variables.
        CurrentReqLinePos = 1
        CurrentTestLinePos = 1
        FinishedProcessing = False
        ChunkEnd = 0
        NumberChunks = 0
        Do
            NextVarPos = InStr(CurrentReqLinePos, RequiredLine, "#")
            If NextVarPos = 0 Then
                FinishedProcessing = True
                NextVarPos = Len(RequiredLine) + 1
            Else
                Var2Pos = InStr(NextVarPos + 1, RequiredLine, "#")
                CurrentVariable = Mid(RequiredLine, NextVarPos + 1, (Var2Pos - 1) - NextVarPos)
            End If

            CheckChunk = Mid(RequiredLine, CurrentReqLinePos, (NextVarPos - 1) - (CurrentReqLinePos - 1))
            ChunkBegin = InStr(CurrentTestLinePos, LCase(TestLine), LCase(CheckChunk))
            ChunkEnd = ChunkBegin + Len(CheckChunk)

            NumberChunks = NumberChunks + 1
            ReDim Preserve ChunksBegin(NumberChunks)
            ReDim Preserve ChunksEnd(NumberChunks)
            ReDim Preserve VarName(NumberChunks)
            ChunksBegin(NumberChunks) = ChunkBegin
            ChunksEnd(NumberChunks) = ChunkEnd
            VarName(NumberChunks) = CurrentVariable

            'Get to end of variable name
            CurrentReqLinePos = Var2Pos + 1

            CurrentTestLinePos = ChunkEnd
        Loop Until FinishedProcessing

        success = True

        'Return values to string variable
        For i = 1 To NumberChunks - 1
            ' If VarName contains array name, change to index number
            If InStr(VarName(i), "[") > 0 Then
                ArrayIndex = GetArrayIndex(VarName(i), ctx)
            Else
                ArrayIndex = 0
            End If

            CurChunk = Mid(TestLine, ChunksEnd(i), ChunksBegin(i + 1) - ChunksEnd(i))

            If BeginsWith(VarName(i), "@") Then
                VarName(i) = GetEverythingAfter(VarName(i), "@")
                ObjID = Disambiguate(CurChunk, _currentRoom & ";" & "inventory", ctx)

                If ObjID = -1 Then
                    If _gameAslVersion >= 391 Then
                        PlayerErrorMessage(PlayerError.BadThing, ctx)
                    Else
                        PlayerErrorMessage(PlayerError.BadItem, ctx)
                    End If
                    ' The Mid$(...,2) and Left$(...,2) removes the initial/final "."
                    _badCmdBefore = Mid(Trim(Left(TestLine, ChunksEnd(i) - 1)), 2)
                    _badCmdAfter = Trim(Mid(TestLine, ChunksBegin(i + 1)))
                    _badCmdAfter = Left(_badCmdAfter, Len(_badCmdAfter) - 1)
                    success = False
                ElseIf ObjID = -2 Then
                    _badCmdBefore = Trim(Left(TestLine, ChunksEnd(i) - 1))
                    _badCmdAfter = Trim(Mid(TestLine, ChunksBegin(i + 1)))
                    success = False
                Else
                    SetStringContents(VarName(i), _objs(ObjID).ObjectName, ctx, ArrayIndex)
                End If
            Else
                SetStringContents(VarName(i), CurChunk, ctx, ArrayIndex)
            End If
        Next i

        Return success
    End Function

    Private Function GetGender(CharacterName As String, Capitalise As Boolean, ctx As Context) As String
        Dim G, GL As String

        If _gameAslVersion >= 281 Then
            G = _objs(GetObjectIDNoAlias(CharacterName)).Gender
        Else
            GL = RetrLine("character", CharacterName, "gender", ctx)

            If GL = "<unfound>" Then
                G = "it "
            Else
                G = RetrieveParameter(GL, ctx) & " "
            End If
        End If

        If Capitalise = True Then G = UCase(Left(G, 1)) & Right(G, Len(G) - 1)
        Return G
    End Function

    Private Function GetStringContents(StringName As String, ctx As Context) As String
        Dim bStringExists As Boolean
        Dim iStringNumber As Integer
        bStringExists = False
        Dim BeginPos, ArrayIndex, EndPos As Integer
        Dim ArrayIndexData As String
        Dim ColonPos As Integer
        Dim B1, B2 As Integer
        Dim ObjName As String
        Dim PropName As String
        Dim ReturnAlias As Boolean
        Dim i As Integer
        ReturnAlias = False
        ArrayIndex = 0

        ' Check for property shortcut
        ColonPos = InStr(StringName, ":")
        If ColonPos <> 0 Then
            ObjName = Trim(Left(StringName, ColonPos - 1))
            PropName = Trim(Mid(StringName, ColonPos + 1))

            B1 = InStr(ObjName, "(")
            If B1 <> 0 Then
                B2 = InStr(B1, ObjName, ")")
                If B2 <> 0 Then
                    ObjName = GetStringContents(Mid(ObjName, B1 + 1, (B2 - B1) - 1), ctx)
                End If
            End If

            Return GetObjectProperty(PropName, GetObjectIDNoAlias(ObjName))
        End If
        If Left(StringName, 1) = "@" Then
            ReturnAlias = True
            StringName = Mid(StringName, 2)
        End If

        If InStr(StringName, "[") <> 0 And InStr(StringName, "]") <> 0 Then
            BeginPos = InStr(StringName, "[")
            EndPos = InStr(StringName, "]")
            ArrayIndexData = Mid(StringName, BeginPos + 1, (EndPos - BeginPos) - 1)
            If IsNumeric(ArrayIndexData) Then
                ArrayIndex = CInt(ArrayIndexData)
            Else
                ArrayIndex = CInt(GetNumericContents(ArrayIndexData, ctx))
                If ArrayIndex = -32767 Then
                    LogASLError("Array index in '" & StringName & "' is not valid. An array index must be either a number or a numeric variable (without surrounding '%' characters)", LogType.WarningError)
                    Return ""
                End If
            End If
            StringName = Left(StringName, BeginPos - 1)
        End If

        ' First, see if the string already exists. If it does,
        ' get its contents. If not, generate an error.

        If _numberStringVariables > 0 Then
            For i = 1 To _numberStringVariables
                If LCase(_stringVariable(i).VariableName) = LCase(StringName) Then
                    iStringNumber = i
                    bStringExists = True
                    i = _numberStringVariables
                End If
            Next i
        End If

        If bStringExists = False Then
            LogASLError("No string variable '" & StringName & "' defined.", LogType.WarningError)
            Return ""
        End If

        If ArrayIndex > _stringVariable(iStringNumber).VariableUBound Then
            LogASLError("Array index of '" & StringName & "[" & Trim(Str(ArrayIndex)) & "]' too big.", LogType.WarningError)
            Return ""
        End If

        ' Now, set the contents
        If Not ReturnAlias Then
            Return _stringVariable(iStringNumber).VariableContents(ArrayIndex)
        Else
            Return _objs(GetObjectIDNoAlias(_stringVariable(iStringNumber).VariableContents(ArrayIndex))).ObjectAlias
        End If
    End Function

    Private Function IsAvailable(ThingString As String, ThingType As Thing, ctx As Context) As Boolean
        ' Returns availability of object/character

        ' split ThingString into character name and room
        ' (thingstring of form name@room)

        Dim i, AtPos As Integer
        Dim CRoom, CName As String

        AtPos = InStr(ThingString, "@")

        ' If no room specified, current room presumed
        If AtPos = 0 Then
            CRoom = _currentRoom
            CName = ThingString
        Else
            CName = Trim(Left(ThingString, AtPos - 1))
            CRoom = Trim(Right(ThingString, Len(ThingString) - AtPos))
        End If

        If ThingType = Thing.Character Then
            For i = 1 To _numberChars
                If LCase(_chars(i).ContainerRoom) = LCase(CRoom) And LCase(_chars(i).ObjectName) = LCase(CName) Then
                    Return _chars(i).Exists
                End If
            Next i
        ElseIf ThingType = Thing.Object Then
            For i = 1 To _numberObjs
                If LCase(_objs(i).ContainerRoom) = LCase(CRoom) And LCase(_objs(i).ObjectName) = LCase(CName) Then
                    Return _objs(i).Exists
                End If
            Next i
        End If
    End Function

    Private Function IsCompatible(TestLine As String, RequiredLine As String) As Boolean
        'Tests to see if TestLine "works" with RequiredLine.
        'For example, if RequiredLine = "read #text#", then the
        'TestLines of "read book" and "read sign" are compatible.
        Dim CurrentReqLinePos, NextVarPos As Integer
        Dim CheckChunk As String
        Dim CurrentTestLinePos As Integer
        Dim FinishedProcessing As Boolean
        Dim Var2Pos As Integer

        ' This avoids "xxx123" being compatible with "xxx".
        TestLine = "�" & Trim(TestLine) & "�"
        RequiredLine = "�" & RequiredLine & "�"

        'Go through RequiredLine in chunks going up to variables.
        CurrentReqLinePos = 1
        CurrentTestLinePos = 1
        FinishedProcessing = False
        Do
            NextVarPos = InStr(CurrentReqLinePos, RequiredLine, "#")
            If NextVarPos = 0 Then
                NextVarPos = Len(RequiredLine) + 1
                FinishedProcessing = True
            Else
                Var2Pos = InStr(NextVarPos + 1, RequiredLine, "#")
            End If

            CheckChunk = Mid(RequiredLine, CurrentReqLinePos, (NextVarPos - 1) - (CurrentReqLinePos - 1))

            If InStr(CurrentTestLinePos, TestLine, CheckChunk) <> 0 Then
                CurrentTestLinePos = InStr(CurrentTestLinePos, TestLine, CheckChunk) + Len(CheckChunk)
            Else
                Return False
            End If

            'Skip to end of variable
            CurrentReqLinePos = Var2Pos + 1
        Loop Until FinishedProcessing

        Return True
    End Function

    Private Function OpenGame(theGameFileName As String) As Boolean
        Dim cdatb, bResult As Boolean
        Dim CurObjVisible As Boolean
        Dim CurObjRoom As String
        Dim PrevQSGVersion As Boolean
        Dim FileData As String = ""
        Dim SavedQSGVersion As String
        Dim i As Integer
        Dim NullData As String = ""
        Dim CData As String = ""
        Dim CName As String
        Dim SemiColonPos, CDat As Integer
        Dim SC2Pos, SC3Pos As Integer
        Dim lines As String() = {}

        _gameLoadMethod = "loaded"

        PrevQSGVersion = False

        If m_data Is Nothing Then
            FileData = System.IO.File.ReadAllText(theGameFileName, System.Text.Encoding.GetEncoding(1252))
        Else
            FileData = System.Text.Encoding.GetEncoding(1252).GetString(m_data.Data)
        End If

        ' Check version
        SavedQSGVersion = Left(FileData, 10)

        If BeginsWith(SavedQSGVersion, "QUEST200.1") Then
            PrevQSGVersion = True
        ElseIf Not BeginsWith(SavedQSGVersion, "QUEST300") Then
            Return False
        End If

        If PrevQSGVersion Then
            lines = FileData.Split({vbCrLf, vbLf}, StringSplitOptions.None)
            _gameFileName = lines(1)
        Else
            InitFileData(FileData)
            NullData = GetNextChunk()

            If m_data Is Nothing Then
                _gameFileName = GetNextChunk()
            Else
                GetNextChunk()
                _gameFileName = m_data.SourceFile
            End If
        End If

        If m_data Is Nothing And Not System.IO.File.Exists(_gameFileName) Then
            _gameFileName = m_player.GetNewGameFile(_gameFileName, "*.asl;*.cas;*.zip")
            If _gameFileName = "" Then Exit Function
        End If

        bResult = InitialiseGame(_gameFileName, True)

        If bResult = False Then
            Return False
        End If

        If Not PrevQSGVersion Then
            ' Open Quest 3.0 saved game file
            _gameLoading = True
            RestoreGameData(FileData)
            _gameLoading = False
        Else
            ' Open Quest 2.x saved game file

            _currentRoom = lines(3)

            ' Start at line 5 as line 4 is always "!c"
            Dim lineNumber As Integer = 5

            Do
                CData = lines(lineNumber)
                lineNumber += 1
                If CData <> "!i" Then
                    SemiColonPos = InStr(CData, ";")
                    CName = Trim(Left(CData, SemiColonPos - 1))
                    CDat = CInt(Right(CData, Len(CData) - SemiColonPos))

                    For i = 1 To _numCollectables
                        If _collectables(i).Name = CName Then
                            _collectables(i).Value = CDat
                            i = _numCollectables
                        End If
                    Next i
                End If
            Loop Until CData = "!i"

            Do
                CData = lines(lineNumber)
                lineNumber += 1
                If CData <> "!o" Then
                    SemiColonPos = InStr(CData, ";")
                    CName = Trim(Left(CData, SemiColonPos - 1))
                    cdatb = IsYes(Right(CData, Len(CData) - SemiColonPos))

                    For i = 1 To _numberItems
                        If _items(i).Name = CName Then
                            _items(i).Got = cdatb
                            i = _numberItems
                        End If
                    Next i
                End If
            Loop Until CData = "!o"

            Do
                CData = lines(lineNumber)
                lineNumber += 1
                If CData <> "!p" Then
                    SemiColonPos = InStr(CData, ";")
                    SC2Pos = InStr(SemiColonPos + 1, CData, ";")
                    SC3Pos = InStr(SC2Pos + 1, CData, ";")

                    CName = Trim(Left(CData, SemiColonPos - 1))
                    cdatb = IsYes(Mid(CData, SemiColonPos + 1, (SC2Pos - SemiColonPos) - 1))
                    CurObjVisible = IsYes(Mid(CData, SC2Pos + 1, (SC3Pos - SC2Pos) - 1))
                    CurObjRoom = Trim(Mid(CData, SC3Pos + 1))

                    For i = 1 To _numberObjs
                        If _objs(i).ObjectName = CName And Not _objs(i).Loaded Then
                            _objs(i).Exists = cdatb
                            _objs(i).Visible = CurObjVisible
                            _objs(i).ContainerRoom = CurObjRoom
                            _objs(i).Loaded = True
                            i = _numberObjs
                        End If
                    Next i
                End If
            Loop Until CData = "!p"

            Do
                CData = lines(lineNumber)
                lineNumber += 1
                If CData <> "!s" Then
                    SemiColonPos = InStr(CData, ";")
                    SC2Pos = InStr(SemiColonPos + 1, CData, ";")
                    SC3Pos = InStr(SC2Pos + 1, CData, ";")

                    CName = Trim(Left(CData, SemiColonPos - 1))
                    cdatb = IsYes(Mid(CData, SemiColonPos + 1, (SC2Pos - SemiColonPos) - 1))
                    CurObjVisible = IsYes(Mid(CData, SC2Pos + 1, (SC3Pos - SC2Pos) - 1))
                    CurObjRoom = Trim(Mid(CData, SC3Pos + 1))

                    For i = 1 To _numberChars
                        If _chars(i).ObjectName = CName Then
                            _chars(i).Exists = cdatb
                            _chars(i).Visible = CurObjVisible
                            _chars(i).ContainerRoom = CurObjRoom
                            i = _numberChars
                        End If
                    Next i
                End If
            Loop Until CData = "!s"

            Do
                CData = lines(lineNumber)
                lineNumber += 1
                If CData <> "!n" Then
                    SemiColonPos = InStr(CData, ";")
                    CName = Trim(Left(CData, SemiColonPos - 1))
                    CData = Right(CData, Len(CData) - SemiColonPos)

                    SetStringContents(CName, CData, _nullContext)
                End If
            Loop Until CData = "!n"

            Do
                CData = lines(lineNumber)
                lineNumber += 1
                If CData <> "!e" Then
                    SemiColonPos = InStr(CData, ";")
                    CName = Trim(Left(CData, SemiColonPos - 1))
                    CData = Right(CData, Len(CData) - SemiColonPos)

                    SetNumericVariableContents(CName, Val(CData), _nullContext)
                End If
            Loop Until CData = "!e"

        End If

        _saveGameFile = theGameFileName

        Return True
    End Function

    Private Function SaveGame(theGameFileName As String, Optional saveFile As Boolean = True) As Byte()
        Dim NewThread As Context = New Context()
        Dim saveData As String

        If _gameAslVersion >= 391 Then ExecuteScript(_beforeSaveScript, NewThread)

        If _gameAslVersion >= 280 Then
            saveData = MakeRestoreData()
        Else
            saveData = MakeRestoreDataV2()
        End If

        If saveFile Then
            System.IO.File.WriteAllText(theGameFileName, saveData, System.Text.Encoding.GetEncoding(1252))
        End If

        _saveGameFile = theGameFileName

        Return System.Text.Encoding.GetEncoding(1252).GetBytes(saveData)
    End Function

    Private Function MakeRestoreDataV2() As String
        Dim lines As New List(Of String)
        Dim i As Integer

        lines.Add("QUEST200.1")
        lines.Add(GetOriginalFilenameForQSG)
        lines.Add(_gameName)
        lines.Add(_currentRoom)

        lines.Add("!c")
        For i = 1 To _numCollectables
            lines.Add(_collectables(i).Name & ";" & Str(_collectables(i).Value))
        Next i

        lines.Add("!i")
        For i = 1 To _numberItems
            lines.Add(_items(i).Name & ";" & YesNo(_items(i).Got))
        Next i

        lines.Add("!o")
        For i = 1 To _numberObjs
            lines.Add(_objs(i).ObjectName & ";" & YesNo(_objs(i).Exists) & ";" & YesNo(_objs(i).Visible) & ";" & _objs(i).ContainerRoom)
        Next i

        lines.Add("!p")
        For i = 1 To _numberChars
            lines.Add(_chars(i).ObjectName & ";" & YesNo(_chars(i).Exists) & ";" & YesNo(_chars(i).Visible) & ";" & _chars(i).ContainerRoom)
        Next i

        lines.Add("!s")
        For i = 1 To _numberStringVariables
            lines.Add(_stringVariable(i).VariableName & ";" & _stringVariable(i).VariableContents(0))
        Next i

        lines.Add("!n")
        For i = 1 To _numberNumericVariables
            lines.Add(_numericVariable(i).VariableName & ";" & Str(CDbl(_numericVariable(i).VariableContents(0))))
        Next i

        lines.Add("!e")

        Return String.Join(vbCrLf, lines)
    End Function

    Private Sub SetAvailability(ThingString As String, ThingExist As Boolean, ctx As Context, Optional ThingType As Thing = Thing.Object)
        ' Sets availability of objects (and characters in ASL<281)

        Dim i, ObjID, AtPos As Integer
        Dim CRoom, CName As String

        Dim FoundObject As Boolean
        If _gameAslVersion >= 281 Then
            FoundObject = False
            For i = 1 To _numberObjs
                If LCase(_objs(i).ObjectName) = LCase(ThingString) Then
                    _objs(i).Exists = ThingExist
                    If ThingExist Then
                        AddToObjectProperties("not hidden", i, ctx)
                    Else
                        AddToObjectProperties("hidden", i, ctx)
                    End If
                    ObjID = i
                    FoundObject = True
                    i = _numberObjs
                End If
            Next i

            If Not FoundObject Then
                LogASLError("Not found object '" & ThingString & "'", LogType.WarningError)
            End If
        Else
            ' split ThingString into character name and room
            ' (thingstring of form name@room)

            AtPos = InStr(ThingString, "@")
            ' If no room specified, currentroom presumed
            If AtPos = 0 Then
                CRoom = _currentRoom
                CName = ThingString
            Else
                CName = Trim(Left(ThingString, AtPos - 1))
                CRoom = Trim(Right(ThingString, Len(ThingString) - AtPos))
            End If
            If ThingType = Thing.Character Then
                For i = 1 To _numberChars
                    If LCase(_chars(i).ContainerRoom) = LCase(CRoom) And LCase(_chars(i).ObjectName) = LCase(CName) Then
                        _chars(i).Exists = ThingExist
                        i = _numberChars + 1
                    End If
                Next i
            ElseIf ThingType = Thing.Object Then
                For i = 1 To _numberObjs
                    If LCase(_objs(i).ContainerRoom) = LCase(CRoom) And LCase(_objs(i).ObjectName) = LCase(CName) Then
                        _objs(i).Exists = ThingExist
                        i = _numberObjs + 1
                    End If
                Next i
            End If
        End If

        UpdateItems(ctx)
        UpdateObjectList(ctx)
    End Sub

    Friend Sub SetStringContents(StringName As String, StringContents As String, ctx As Context, Optional ArrayIndex As Integer = 0)
        Dim bStringExists As Boolean
        Dim iStringNumber, i As Integer
        Dim StringTitle, OnChangeScript As String
        Dim BracketPos As Integer
        bStringExists = False

        If StringName = "" Then
            LogASLError("Internal error - tried to set empty string name to '" & StringContents & "'", LogType.WarningError)
            Exit Sub
        End If

        If _gameAslVersion >= 281 Then
            BracketPos = InStr(StringName, "[")
            If BracketPos <> 0 Then
                ArrayIndex = GetArrayIndex(StringName, ctx)
                StringName = Left(StringName, BracketPos - 1)
            End If
        End If

        If ArrayIndex < 0 Then
            LogASLError("'" & StringName & "[" & Trim(Str(ArrayIndex)) & "]' is invalid - did not assign to array", LogType.WarningError)
            Exit Sub
        End If

        ' First, see if the string already exists. If it does,
        ' modify it. If not, create it.

        If _numberStringVariables > 0 Then
            For i = 1 To _numberStringVariables
                If LCase(_stringVariable(i).VariableName) = LCase(StringName) Then
                    iStringNumber = i
                    bStringExists = True
                    i = _numberStringVariables
                End If
            Next i
        End If

        If bStringExists = False Then
            _numberStringVariables = _numberStringVariables + 1
            iStringNumber = _numberStringVariables
            ReDim Preserve _stringVariable(iStringNumber)
            _stringVariable(iStringNumber) = New VariableType

            For i = 0 To ArrayIndex
                StringTitle = StringName
                If ArrayIndex <> 0 Then StringTitle = StringTitle & "[" & Trim(Str(i)) & "]"
            Next i
            _stringVariable(iStringNumber).VariableUBound = ArrayIndex
        End If

        If ArrayIndex > _stringVariable(iStringNumber).VariableUBound Then
            ReDim Preserve _stringVariable(iStringNumber).VariableContents(ArrayIndex)
            For i = _stringVariable(iStringNumber).VariableUBound + 1 To ArrayIndex
                StringTitle = StringName
                If ArrayIndex <> 0 Then StringTitle = StringTitle & "[" & Trim(Str(i)) & "]"
            Next i
            _stringVariable(iStringNumber).VariableUBound = ArrayIndex
        End If

        ' Now, set the contents
        _stringVariable(iStringNumber).VariableName = StringName
        ReDim Preserve _stringVariable(iStringNumber).VariableContents(_stringVariable(iStringNumber).VariableUBound)
        _stringVariable(iStringNumber).VariableContents(ArrayIndex) = StringContents

        If _stringVariable(iStringNumber).OnChangeScript <> "" Then
            OnChangeScript = _stringVariable(iStringNumber).OnChangeScript
            ExecuteScript(OnChangeScript, ctx)
        End If

        If _stringVariable(iStringNumber).DisplayString <> "" Then
            UpdateStatusVars(ctx)
        End If
    End Sub

    Private Sub SetUpCharObjectInfo()
        Dim cdf As DefineBlock
        Dim DefaultExists As Boolean
        Dim PropertyData As PropertiesActions
        Dim DefaultProperties As New PropertiesActions
        Dim RestOfLine As String
        Dim i As Integer
        Dim OrigCRoomName, CRoomName As String
        Dim k, j, e As Integer

        _numberChars = 0

        ' see if define type <default> exists:
        DefaultExists = False
        For i = 1 To _numberSections
            If Trim(_lines(_defineBlocks(i).StartLine)) = "define type <default>" Then
                DefaultExists = True
                DefaultProperties = GetPropertiesInType("default")
                i = _numberSections
            End If
        Next i

        For i = 1 To _numberSections
            cdf = _defineBlocks(i)
            If BeginsWith(_lines(cdf.StartLine), "define room") Or BeginsWith(_lines(cdf.StartLine), "define game") Or BeginsWith(_lines(cdf.StartLine), "define object ") Then
                If BeginsWith(_lines(cdf.StartLine), "define room") Then
                    OrigCRoomName = RetrieveParameter(_lines(cdf.StartLine), _nullContext)
                Else
                    OrigCRoomName = ""
                End If

                Dim startLine As Integer = cdf.StartLine
                Dim endLine As Integer = cdf.EndLine

                If BeginsWith(_lines(cdf.StartLine), "define object ") Then
                    startLine = startLine - 1
                    endLine = endLine + 1
                End If

                For j = startLine + 1 To endLine - 1
                    If BeginsWith(_lines(j), "define object") Then
                        CRoomName = OrigCRoomName

                        _numberObjs = _numberObjs + 1
                        ReDim Preserve _objs(_numberObjs)
                        _objs(_numberObjs) = New ObjectType

                        Dim o = _objs(_numberObjs)

                        o.ObjectName = RetrieveParameter(_lines(j), _nullContext)
                        o.ObjectAlias = o.ObjectName
                        o.DefinitionSectionStart = j
                        o.ContainerRoom = CRoomName
                        o.Visible = True
                        o.Gender = "it"
                        o.Article = "it"

                        o.Take.Type = TextActionType.Nothing

                        If DefaultExists Then
                            AddToObjectProperties(DefaultProperties.Properties, _numberObjs, _nullContext)
                            For k = 1 To DefaultProperties.NumberActions
                                AddObjectAction(_numberObjs, DefaultProperties.Actions(k).ActionName, DefaultProperties.Actions(k).Script)
                            Next k
                        End If

                        If _gameAslVersion >= 391 Then AddToObjectProperties("list", _numberObjs, _nullContext)

                        e = 0
                        Do
                            j = j + 1
                            If Trim(_lines(j)) = "hidden" Then
                                o.Exists = False
                                e = 1
                                If _gameAslVersion >= 311 Then AddToObjectProperties("hidden", _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "startin ") And CRoomName = "__UNKNOWN" Then
                                CRoomName = RetrieveParameter(_lines(j), _nullContext)
                            ElseIf BeginsWith(_lines(j), "prefix ") Then
                                o.Prefix = RetrieveParameter(_lines(j), _nullContext) & " "
                                If _gameAslVersion >= 311 Then AddToObjectProperties("prefix=" & o.Prefix, _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "suffix ") Then
                                o.Suffix = RetrieveParameter(_lines(j), _nullContext)
                                If _gameAslVersion >= 311 Then AddToObjectProperties("suffix=" & o.Suffix, _numberObjs, _nullContext)
                            ElseIf Trim(_lines(j)) = "invisible" Then
                                o.Visible = False
                                If _gameAslVersion >= 311 Then AddToObjectProperties("invisible", _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "alias ") Then
                                o.ObjectAlias = RetrieveParameter(_lines(j), _nullContext)
                                If _gameAslVersion >= 311 Then AddToObjectProperties("alias=" & o.ObjectAlias, _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "alt ") Then
                                AddToObjectAltNames(RetrieveParameter(_lines(j), _nullContext), _numberObjs)
                            ElseIf BeginsWith(_lines(j), "detail ") Then
                                o.Detail = RetrieveParameter(_lines(j), _nullContext)
                                If _gameAslVersion >= 311 Then AddToObjectProperties("detail=" & o.Detail, _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "gender ") Then
                                o.Gender = RetrieveParameter(_lines(j), _nullContext)
                                If _gameAslVersion >= 311 Then AddToObjectProperties("gender=" & o.Gender, _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "article ") Then
                                o.Article = RetrieveParameter(_lines(j), _nullContext)
                                If _gameAslVersion >= 311 Then AddToObjectProperties("article=" & o.Article, _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "gain ") Then
                                o.GainScript = GetEverythingAfter(_lines(j), "gain ")
                                AddObjectAction(_numberObjs, "gain", o.GainScript)
                            ElseIf BeginsWith(_lines(j), "lose ") Then
                                o.LoseScript = GetEverythingAfter(_lines(j), "lose ")
                                AddObjectAction(_numberObjs, "lose", o.LoseScript)
                            ElseIf BeginsWith(_lines(j), "displaytype ") Then
                                o.DisplayType = RetrieveParameter(_lines(j), _nullContext)
                                If _gameAslVersion >= 311 Then AddToObjectProperties("displaytype=" & o.DisplayType, _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "look ") Then
                                If _gameAslVersion >= 311 Then
                                    RestOfLine = GetEverythingAfter(_lines(j), "look ")
                                    If Left(RestOfLine, 1) = "<" Then
                                        AddToObjectProperties("look=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                                    Else
                                        AddObjectAction(_numberObjs, "look", RestOfLine)
                                    End If
                                End If
                            ElseIf BeginsWith(_lines(j), "examine ") Then
                                If _gameAslVersion >= 311 Then
                                    RestOfLine = GetEverythingAfter(_lines(j), "examine ")
                                    If Left(RestOfLine, 1) = "<" Then
                                        AddToObjectProperties("examine=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                                    Else
                                        AddObjectAction(_numberObjs, "examine", RestOfLine)
                                    End If
                                End If
                            ElseIf _gameAslVersion >= 311 And BeginsWith(_lines(j), "speak ") Then
                                RestOfLine = GetEverythingAfter(_lines(j), "speak ")
                                If Left(RestOfLine, 1) = "<" Then
                                    AddToObjectProperties("speak=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                                Else
                                    AddObjectAction(_numberObjs, "speak", RestOfLine)
                                End If
                            ElseIf BeginsWith(_lines(j), "properties ") Then
                                AddToObjectProperties(RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "type ") Then
                                o.NumberTypesIncluded = o.NumberTypesIncluded + 1
                                ReDim Preserve o.TypesIncluded(o.NumberTypesIncluded)
                                o.TypesIncluded(o.NumberTypesIncluded) = RetrieveParameter(_lines(j), _nullContext)

                                PropertyData = GetPropertiesInType(RetrieveParameter(_lines(j), _nullContext))
                                AddToObjectProperties(PropertyData.Properties, _numberObjs, _nullContext)
                                For k = 1 To PropertyData.NumberActions
                                    AddObjectAction(_numberObjs, PropertyData.Actions(k).ActionName, PropertyData.Actions(k).Script)
                                Next k

                                ReDim Preserve o.TypesIncluded(o.NumberTypesIncluded + PropertyData.NumberTypesIncluded)
                                For k = 1 To PropertyData.NumberTypesIncluded
                                    o.TypesIncluded(k + o.NumberTypesIncluded) = PropertyData.TypesIncluded(k)
                                Next k
                                o.NumberTypesIncluded = o.NumberTypesIncluded + PropertyData.NumberTypesIncluded
                            ElseIf BeginsWith(_lines(j), "action ") Then
                                AddToObjectActions(GetEverythingAfter(_lines(j), "action "), _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "use ") Then
                                AddToUseInfo(_numberObjs, GetEverythingAfter(_lines(j), "use "))
                            ElseIf BeginsWith(_lines(j), "give ") Then
                                AddToGiveInfo(_numberObjs, GetEverythingAfter(_lines(j), "give "))
                            ElseIf Trim(_lines(j)) = "take" Then
                                o.Take.Type = TextActionType.Default
                                AddToObjectProperties("take", _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "take ") Then
                                If Left(GetEverythingAfter(_lines(j), "take "), 1) = "<" Then
                                    o.Take.Type = TextActionType.Text
                                    o.Take.Data = RetrieveParameter(_lines(j), _nullContext)

                                    AddToObjectProperties("take=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                                Else
                                    o.Take.Type = TextActionType.Script
                                    RestOfLine = GetEverythingAfter(_lines(j), "take ")
                                    o.Take.Data = RestOfLine

                                    AddObjectAction(_numberObjs, "take", RestOfLine)
                                End If
                            ElseIf Trim(_lines(j)) = "container" Then
                                If _gameAslVersion >= 391 Then AddToObjectProperties("container", _numberObjs, _nullContext)
                            ElseIf Trim(_lines(j)) = "surface" Then
                                If _gameAslVersion >= 391 Then
                                    AddToObjectProperties("container", _numberObjs, _nullContext)
                                    AddToObjectProperties("surface", _numberObjs, _nullContext)
                                End If
                            ElseIf Trim(_lines(j)) = "opened" Then
                                If _gameAslVersion >= 391 Then AddToObjectProperties("opened", _numberObjs, _nullContext)
                            ElseIf Trim(_lines(j)) = "transparent" Then
                                If _gameAslVersion >= 391 Then AddToObjectProperties("transparent", _numberObjs, _nullContext)
                            ElseIf Trim(_lines(j)) = "open" Then
                                AddToObjectProperties("open", _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "open ") Then
                                If Left(GetEverythingAfter(_lines(j), "open "), 1) = "<" Then
                                    AddToObjectProperties("open=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                                Else
                                    RestOfLine = GetEverythingAfter(_lines(j), "open ")
                                    AddObjectAction(_numberObjs, "open", RestOfLine)
                                End If
                            ElseIf Trim(_lines(j)) = "close" Then
                                AddToObjectProperties("close", _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "close ") Then
                                If Left(GetEverythingAfter(_lines(j), "close "), 1) = "<" Then
                                    AddToObjectProperties("close=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                                Else
                                    RestOfLine = GetEverythingAfter(_lines(j), "close ")
                                    AddObjectAction(_numberObjs, "close", RestOfLine)
                                End If
                            ElseIf Trim(_lines(j)) = "add" Then
                                AddToObjectProperties("add", _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "add ") Then
                                If Left(GetEverythingAfter(_lines(j), "add "), 1) = "<" Then
                                    AddToObjectProperties("add=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                                Else
                                    RestOfLine = GetEverythingAfter(_lines(j), "add ")
                                    AddObjectAction(_numberObjs, "add", RestOfLine)
                                End If
                            ElseIf Trim(_lines(j)) = "remove" Then
                                AddToObjectProperties("remove", _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "remove ") Then
                                If Left(GetEverythingAfter(_lines(j), "remove "), 1) = "<" Then
                                    AddToObjectProperties("remove=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                                Else
                                    RestOfLine = GetEverythingAfter(_lines(j), "remove ")
                                    AddObjectAction(_numberObjs, "remove", RestOfLine)
                                End If
                            ElseIf BeginsWith(_lines(j), "parent ") Then
                                AddToObjectProperties("parent=" & RetrieveParameter(_lines(j), _nullContext), _numberObjs, _nullContext)
                            ElseIf BeginsWith(_lines(j), "list") Then
                                ProcessListInfo(_lines(j), _numberObjs)
                            End If

                        Loop Until Trim(_lines(j)) = "end define"

                        o.DefinitionSectionEnd = j
                        If e = 0 Then o.Exists = True
                    ElseIf _gameAslVersion <= 280 And BeginsWith(_lines(j), "define character") Then
                        CRoomName = OrigCRoomName
                        _numberChars = _numberChars + 1
                        ReDim Preserve _chars(_numberChars)
                        _chars(_numberChars) = New ObjectType
                        _chars(_numberChars).ObjectName = RetrieveParameter(_lines(j), _nullContext)
                        _chars(_numberChars).DefinitionSectionStart = j
                        _chars(_numberChars).ContainerRoom = ""
                        _chars(_numberChars).Visible = True
                        e = 0
                        Do
                            j = j + 1
                            If Trim(_lines(j)) = "hidden" Then
                                _chars(_numberChars).Exists = False
                                e = 1
                            ElseIf BeginsWith(_lines(j), "startin ") And CRoomName = "__UNKNOWN" Then
                                CRoomName = RetrieveParameter(_lines(j), _nullContext)
                            ElseIf BeginsWith(_lines(j), "prefix ") Then
                                _chars(_numberChars).Prefix = RetrieveParameter(_lines(j), _nullContext) & " "
                            ElseIf BeginsWith(_lines(j), "suffix ") Then
                                _chars(_numberChars).Suffix = " " & RetrieveParameter(_lines(j), _nullContext)
                            ElseIf Trim(_lines(j)) = "invisible" Then
                                _chars(_numberChars).Visible = False
                            ElseIf BeginsWith(_lines(j), "alias ") Then
                                _chars(_numberChars).ObjectAlias = RetrieveParameter(_lines(j), _nullContext)
                            ElseIf BeginsWith(_lines(j), "detail ") Then
                                _chars(_numberChars).Detail = RetrieveParameter(_lines(j), _nullContext)
                            End If

                            _chars(_numberChars).ContainerRoom = CRoomName

                        Loop Until Trim(_lines(j)) = "end define"

                        _chars(_numberChars).DefinitionSectionEnd = j
                        If e = 0 Then _chars(_numberChars).Exists = True
                    End If
                Next j
            End If
        Next i

        UpdateVisibilityInContainers(_nullContext)
    End Sub

    Private Sub ShowGameAbout(ctx As Context)
        Dim GameAuthor, GameVersion, GameCopy As String
        Dim GameInfo As String

        GameVersion = FindStatement(GetDefineBlock("game"), "game version")
        GameAuthor = FindStatement(GetDefineBlock("game"), "game author")
        GameCopy = FindStatement(GetDefineBlock("game"), "game copyright")
        GameInfo = FindStatement(GetDefineBlock("game"), "game info")

        Print("|bGame name:|cl  " & _gameName & "|cb|xb", ctx)
        If GameVersion <> "" Then Print("|bVersion:|xb    " & GameVersion, ctx)
        If GameAuthor <> "" Then Print("|bAuthor:|xb     " & GameAuthor, ctx)
        If GameCopy <> "" Then Print("|bCopyright:|xb  " & GameCopy, ctx)

        If GameInfo <> "" Then
            Print("", ctx)
            Print(GameInfo, ctx)
        End If
    End Sub

    Private Sub ShowPicture(sFileName As String)
        ' In Quest 4.x this function would be used for showing a picture in a popup window, but
        ' this is no longer supported - ALL images are displayed in-line with the game text. Any
        ' image caption is displayed as text, and any image size specified is ignored.

        Dim SizeString As String
        Dim Caption As String = ""

        If InStr(sFileName, ";") <> 0 Then
            Caption = Trim(Mid(sFileName, InStr(sFileName, ";") + 1))
            sFileName = Trim(Left(sFileName, InStr(sFileName, ";") - 1))
        End If

        If InStr(sFileName, "@") <> 0 Then
            SizeString = Trim(Mid(sFileName, InStr(sFileName, "@") + 1))
            sFileName = Trim(Left(sFileName, InStr(sFileName, "@") - 1))
        End If

        If Caption.Length > 0 Then Print(Caption, _nullContext)

        ShowPictureInText(sFileName)
    End Sub

    Private Sub ShowRoomInfo(Room As String, ctx As Context, Optional NoPrint As Boolean = False)
        If _gameAslVersion < 280 Then
            ShowRoomInfoV2(Room)
            Exit Sub
        End If

        Dim RoomDisplayText As String = ""
        Dim DescTagExist As Boolean
        Dim gameblock As DefineBlock
        Dim DoorwayString, RoomAlias As String
        Dim RoomID As Integer
        Dim FinishedFindingCommas As Boolean
        Dim Prefix, RoomDisplayName As String
        Dim RoomDisplayNameNoFormat, InDescription As String
        Dim VisibleObjects As String = ""
        Dim VisibleObjectsNoFormat As String
        Dim i As Integer
        Dim PlaceList As String
        Dim LastComma, OldLastComma As Integer
        Dim DescType As Integer
        Dim DescLine As String = ""
        Dim ShowLookText As Boolean
        Dim LookDesc As String = ""
        Dim ObjLook As String
        Dim ObjSuffix As String

        gameblock = GetDefineBlock("game")

        _currentRoom = Room
        RoomID = GetRoomID(_currentRoom, ctx)

        If RoomID = 0 Then Exit Sub

        ' FIRST LINE - YOU ARE IN... ***********************************************

        RoomAlias = _rooms(RoomID).RoomAlias
        If RoomAlias = "" Then RoomAlias = _rooms(RoomID).RoomName

        Prefix = _rooms(RoomID).Prefix

        If Prefix = "" Then
            RoomDisplayName = "|cr" & RoomAlias & "|cb"
            RoomDisplayNameNoFormat = RoomAlias ' No formatting version, for label
        Else
            RoomDisplayName = Prefix & " |cr" & RoomAlias & "|cb"
            RoomDisplayNameNoFormat = Prefix & " " & RoomAlias
        End If

        InDescription = _rooms(RoomID).InDescription

        If InDescription <> "" Then
            ' Print player's location according to indescription:
            If Right(InDescription, 1) = ":" Then
                ' if line ends with a colon, add place name:
                RoomDisplayText = RoomDisplayText & Left(InDescription, Len(InDescription) - 1) & " " & RoomDisplayName & "." & vbCrLf
            Else
                ' otherwise, just print the indescription line:
                RoomDisplayText = RoomDisplayText & InDescription & vbCrLf
            End If
        Else
            ' if no indescription line, print the default.
            RoomDisplayText = RoomDisplayText & "You are in " & RoomDisplayName & "." & vbCrLf
        End If

        m_player.LocationUpdated(UCase(Left(RoomAlias, 1)) & Mid(RoomAlias, 2))

        SetStringContents("quest.formatroom", RoomDisplayNameNoFormat, ctx)

        ' SHOW OBJECTS *************************************************************

        VisibleObjectsNoFormat = ""

        Dim colVisibleObjects As New List(Of Integer) ' of object IDs
        Dim lCount As Integer

        For i = 1 To _numberObjs
            If LCase(_objs(i).ContainerRoom) = LCase(Room) And _objs(i).Exists And _objs(i).Visible And Not _objs(i).IsExit Then
                colVisibleObjects.Add(i)
            End If
        Next i

        For Each objId As Integer In colVisibleObjects
            ObjSuffix = _objs(objId).Suffix
            If ObjSuffix <> "" Then ObjSuffix = " " & ObjSuffix

            If _objs(objId).ObjectAlias = "" Then
                VisibleObjects = VisibleObjects & _objs(objId).Prefix & "|b" & _objs(objId).ObjectName & "|xb" & ObjSuffix
                VisibleObjectsNoFormat = VisibleObjectsNoFormat & _objs(objId).Prefix & _objs(objId).ObjectName
            Else
                VisibleObjects = VisibleObjects & _objs(objId).Prefix & "|b" & _objs(objId).ObjectAlias & "|xb" & ObjSuffix
                VisibleObjectsNoFormat = VisibleObjectsNoFormat & _objs(objId).Prefix & _objs(objId).ObjectAlias
            End If

            lCount = lCount + 1
            If lCount < colVisibleObjects.Count() - 1 Then
                VisibleObjects = VisibleObjects & ", "
                VisibleObjectsNoFormat = VisibleObjectsNoFormat & ", "
            ElseIf lCount = colVisibleObjects.Count() - 1 Then
                VisibleObjects = VisibleObjects & " and "
                VisibleObjectsNoFormat = VisibleObjectsNoFormat & ", "
            End If
        Next

        If colVisibleObjects.Count() > 0 Then

            SetStringContents("quest.formatobjects", VisibleObjects, ctx)

            VisibleObjects = "There is " & VisibleObjects & " here."

            SetStringContents("quest.objects", VisibleObjectsNoFormat, ctx)

            RoomDisplayText = RoomDisplayText & VisibleObjects & vbCrLf
        Else
            SetStringContents("quest.objects", "", ctx)
            SetStringContents("quest.formatobjects", "", ctx)
        End If

        ' SHOW EXITS ***************************************************************

        DoorwayString = UpdateDoorways(RoomID, ctx)

        If _gameAslVersion < 410 Then
            PlaceList = GetGoToExits(RoomID, ctx)

            If PlaceList <> "" Then
                'strip final comma
                PlaceList = Left(PlaceList, Len(PlaceList) - 2)

                'if there is still a comma here, there is more than
                'one place, so add the word "or" before the last one.
                If InStr(PlaceList, ",") > 0 Then
                    LastComma = 0
                    FinishedFindingCommas = False
                    Do
                        OldLastComma = LastComma
                        LastComma = InStr(LastComma + 1, PlaceList, ",")
                        If LastComma = 0 Then
                            FinishedFindingCommas = True
                            LastComma = OldLastComma
                        End If
                    Loop Until FinishedFindingCommas

                    PlaceList = Left(PlaceList, LastComma - 1) & " or" & Right(PlaceList, Len(PlaceList) - LastComma)
                End If

                RoomDisplayText = RoomDisplayText & "You can go to " & PlaceList & "." & vbCrLf
                SetStringContents("quest.doorways.places", PlaceList, ctx)
            Else
                SetStringContents("quest.doorways.places", "", ctx)
            End If
        End If

        ' GET "LOOK" DESCRIPTION (but don't print it yet) **************************

        ObjLook = GetObjectProperty("look", _rooms(RoomID).ObjID, , False)

        If ObjLook = "" Then
            If _rooms(RoomID).Look <> "" Then
                LookDesc = _rooms(RoomID).Look
            End If
        Else
            LookDesc = ObjLook
        End If

        SetStringContents("quest.lookdesc", LookDesc, ctx)


        ' FIND DESCRIPTION TAG, OR ACTION ******************************************

        ' In Quest versions prior to 3.1, with any custom description, the "look"
        ' text was always displayed after the "description" tag was printed/executed.
        ' In Quest 3.1 and later, it isn't - descriptions should print the look
        ' tag themselves when and where necessary.

        ShowLookText = True

        If _rooms(RoomID).Description.Data <> "" Then
            DescLine = _rooms(RoomID).Description.Data
            DescType = _rooms(RoomID).Description.Type
            DescTagExist = True
        Else
            DescTagExist = False
        End If

        If DescTagExist = False Then
            'Look in the "define game" block:
            For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
                If BeginsWith(_lines(i), "description ") Then
                    DescLine = GetEverythingAfter(_lines(i), "description ")
                    DescTagExist = True
                    If Left(DescLine, 1) = "<" Then
                        DescLine = RetrieveParameter(DescLine, ctx)
                        DescType = TextActionType.Text
                    Else
                        DescType = TextActionType.Script
                    End If
                    i = gameblock.EndLine
                End If
            Next i
        End If

        If DescTagExist And _gameAslVersion >= 310 Then
            ShowLookText = False
        End If

        If Not NoPrint Then
            If DescTagExist = False Then
                'Remove final vbCrLf:
                RoomDisplayText = Left(RoomDisplayText, Len(RoomDisplayText) - 2)
                Print(RoomDisplayText, ctx)
                If DoorwayString <> "" Then Print(DoorwayString, ctx)
            Else
                'execute description tag:
                'If no script, just print the tag's parameter.
                'Otherwise, execute it as ASL script:

                If DescType = TextActionType.Text Then
                    Print(DescLine, ctx)
                Else
                    ExecuteScript(DescLine, ctx)
                End If
            End If

            UpdateObjectList(ctx)

            ' SHOW "LOOK" DESCRIPTION **************************************************

            If ShowLookText Then
                If LookDesc <> "" Then
                    Print(LookDesc, ctx)
                End If
            End If
        End If

    End Sub

    Private Sub CheckCollectable(ColNum As Integer)
        ' Checks to see whether a collectable item has exceeded
        ' its range - if so, it resets the number to the nearest
        ' valid number. It's a handy quick way of making sure that
        ' a player's health doesn't reach 101%, for example.

        Dim maxn, n, minn As Double
        Dim M As Integer
        Dim T As String

        T = _collectables(ColNum).Type
        n = _collectables(ColNum).Value

        If T = "%" And n > 100 Then n = 100
        If (T = "%" Or T = "p") And n < 0 Then n = 0
        If InStr(T, "r") > 0 Then
            If InStr(T, "r") = 1 Then
                maxn = Val(Mid(T, Len(T) - 1))
                M = 1
            ElseIf InStr(T, "r") = Len(T) Then
                minn = Val(Left(T, Len(T) - 1))
                M = 2
            Else
                minn = Val(Left(T, InStr(T, "r") - 1))
                maxn = Val(Mid(T, InStr(T, "r") + 1))
                M = 3
            End If

            If (M = 1 Or M = 3) And n > maxn Then n = maxn
            If (M = 2 Or M = 3) And n < minn Then n = minn
        End If

        _collectables(ColNum).Value = n

    End Sub

    Private Function DisplayCollectableInfo(ColNum As Integer) As String
        Dim FirstBit, D, NextBit As String
        Dim ExcPos As Integer
        Dim FirstStarPos, SecondStarPos As Integer
        Dim AfterStar, BeforeStar, BetweenStar As String

        If _collectables(ColNum).Display = "<def>" Then
            D = "You have " & Trim(Str(_collectables(ColNum).Value)) & " " & _collectables(ColNum).Name
        ElseIf _collectables(ColNum).Display = "" Then
            D = "<null>"
        Else
            ExcPos = InStr(_collectables(ColNum).Display, "!")
            If ExcPos = 0 Then
                D = _collectables(ColNum).Display
            Else
                FirstBit = Left(_collectables(ColNum).Display, ExcPos - 1)
                NextBit = Right(_collectables(ColNum).Display, Len(_collectables(ColNum).Display) - ExcPos)
                D = FirstBit & Trim(Str(_collectables(ColNum).Value)) & NextBit
            End If

            If InStr(D, "*") > 0 Then
                FirstStarPos = InStr(D, "*")
                SecondStarPos = InStr(FirstStarPos + 1, D, "*")
                BeforeStar = Left(D, FirstStarPos - 1)
                AfterStar = Mid(D, SecondStarPos + 1)
                BetweenStar = Mid(D, FirstStarPos + 1, (SecondStarPos - FirstStarPos) - 1)

                If _collectables(ColNum).Value <> 1 Then
                    D = BeforeStar & BetweenStar & AfterStar
                Else
                    D = BeforeStar & AfterStar
                End If
            End If
        End If

        If _collectables(ColNum).Value = 0 And _collectables(ColNum).DisplayWhenZero = False Then
            D = "<null>"
        End If

        Return D
    End Function

    Private Sub DisplayTextSection(sectionname As String, ctx As Context, Optional OutputTo As String = "normal")
        Dim textblock As DefineBlock
        textblock = DefineBlockParam("text", sectionname)
        Dim i As Integer

        If textblock.StartLine <> 0 Then
            For i = textblock.StartLine + 1 To textblock.EndLine - 1
                If _gameAslVersion >= 392 Then
                    ' Convert string variables etc.
                    Print(RetrieveParameter("<" & _lines(i) & ">", ctx), ctx, OutputTo)
                Else
                    Print(_lines(i), ctx, OutputTo)
                End If
            Next i

            Print("", ctx)
        End If
    End Sub

    ' Returns true if the system is ready to process a new command after completion - so it will be
    ' in most cases, except when ExecCommand just caused an "enter" script command to complete

    Private Function ExecCommand(thecommand As String, ctx As Context, Optional EchoCommand As Boolean = True, Optional RunUserCommand As Boolean = True, Optional DontSetIt As Boolean = False) As Boolean
        Dim cmd As String
        Dim EnteredHelpCommand As Boolean
        Dim RoomID As Integer
        Dim GlobalOverride As Boolean
        Dim OldBadCmdBefore As String
        Dim CP, i, n As Integer
        Dim SkipAfterTurn As Boolean
        Dim G, D, c, P, T As String
        Dim j As Integer

        SkipAfterTurn = False
        thecommand = StripCodes(thecommand)

        OldBadCmdBefore = _badCmdBefore

        RoomID = GetRoomID(_currentRoom, ctx)
        EnteredHelpCommand = False

        If thecommand = "" Then Return True

        cmd = LCase(thecommand)

        SyncLock _commandLock
            If _commandOverrideModeOn Then
                ' Commands have been overridden for this command,
                ' so put input into previously specified variable
                ' and exit:

                SetStringContents(_commandOverrideVariable, thecommand, ctx)
                System.Threading.Monitor.PulseAll(_commandLock)
                Return False
            End If
        End SyncLock

        Dim UserCommandReturn As Boolean
        Dim newcommand As String

        If EchoCommand = True Then
            Print("> " & thecommand, ctx, , True)
        End If

        thecommand = LCase(thecommand)

        SetStringContents("quest.originalcommand", thecommand, ctx)

        newcommand = " " & thecommand & " "

        ' Convert synonyms:
        For i = 1 To _numberSynonyms
            CP = 1
            Do
                n = InStr(CP, newcommand, " " & _synonyms(i).OriginalWord & " ")
                If n <> 0 Then
                    newcommand = Left(newcommand, n - 1) & " " & _synonyms(i).ConvertTo & " " & Mid(newcommand, n + Len(_synonyms(i).OriginalWord) + 2)
                    CP = n + 1
                End If
            Loop Until n = 0
        Next i

        'strip starting and ending spaces
        thecommand = Mid(newcommand, 2, Len(newcommand) - 2)

        SetStringContents("quest.command", thecommand, ctx)

        ' Execute any "beforeturn" script:

        Dim NewThread As Context = CopyContext(ctx)
        GlobalOverride = False

        ' RoomID is 0 if there are no rooms in the game. Unlikely, but we get an RTE otherwise.
        If RoomID <> 0 Then
            If _rooms(RoomID).BeforeTurnScript <> "" Then
                If BeginsWith(_rooms(RoomID).BeforeTurnScript, "override") Then
                    ExecuteScript(GetEverythingAfter(_rooms(RoomID).BeforeTurnScript, "override"), NewThread)
                    GlobalOverride = True
                Else
                    ExecuteScript(_rooms(RoomID).BeforeTurnScript, NewThread)
                End If
            End If
        End If
        If _beforeTurnScript <> "" And GlobalOverride = False Then ExecuteScript(_beforeTurnScript, NewThread)

        ' In executing BeforeTurn script, "dontprocess" sets ctx.DontProcessCommand,
        ' in which case we don't process the command.

        If Not NewThread.DontProcessCommand Then
            'Try to execute user defined command, if allowed:

            UserCommandReturn = False
            If RunUserCommand = True Then
                UserCommandReturn = ExecUserCommand(thecommand, ctx)

                If Not UserCommandReturn Then
                    UserCommandReturn = ExecVerb(thecommand, ctx)
                End If

                If Not UserCommandReturn Then
                    ' Try command defined by a library
                    UserCommandReturn = ExecUserCommand(thecommand, ctx, True)
                End If

                If Not UserCommandReturn Then
                    ' Try verb defined by a library
                    UserCommandReturn = ExecVerb(thecommand, ctx, True)
                End If
            End If

            thecommand = LCase(thecommand)
        Else
            ' Set the UserCommand flag to fudge not processing any more commands
            UserCommandReturn = True
        End If

        Dim InvList As String = "", InventoryPlace As String
        Dim LastComma As Integer
        Dim ThisComma, CurPos As Integer
        If UserCommandReturn = False Then

            If CmdStartsWith(thecommand, "speak to ") Then
                c = GetEverythingAfter(thecommand, "speak to ")
                ExecSpeak(c, ctx)
            ElseIf CmdStartsWith(thecommand, "talk to ") Then
                c = GetEverythingAfter(thecommand, "talk to ")
                ExecSpeak(c, ctx)
            ElseIf cmd = "exit" Or cmd = "out" Or cmd = "leave" Then
                LeaveRoom(ctx)
                _lastIt = 0
            ElseIf cmd = "north" Or cmd = "south" Or cmd = "east" Or cmd = "west" Then
                GoDirection(thecommand, ctx)
                _lastIt = 0
            ElseIf cmd = "n" Or cmd = "s" Or cmd = "w" Or cmd = "e" Then
                Select Case InStr("nswe", cmd)
                    Case 1
                        GoDirection("north", ctx)
                    Case 2
                        GoDirection("south", ctx)
                    Case 3
                        GoDirection("west", ctx)
                    Case 4
                        GoDirection("east", ctx)
                End Select
                _lastIt = 0
            ElseIf cmd = "ne" Or cmd = "northeast" Or cmd = "north-east" Or cmd = "north east" Or cmd = "go ne" Or cmd = "go northeast" Or cmd = "go north-east" Or cmd = "go north east" Then
                GoDirection("northeast", ctx)
                _lastIt = 0
            ElseIf cmd = "nw" Or cmd = "northwest" Or cmd = "north-west" Or cmd = "north west" Or cmd = "go nw" Or cmd = "go northwest" Or cmd = "go north-west" Or cmd = "go north west" Then
                GoDirection("northwest", ctx)
                _lastIt = 0
            ElseIf cmd = "se" Or cmd = "southeast" Or cmd = "south-east" Or cmd = "south east" Or cmd = "go se" Or cmd = "go southeast" Or cmd = "go south-east" Or cmd = "go south east" Then
                GoDirection("southeast", ctx)
                _lastIt = 0
            ElseIf cmd = "sw" Or cmd = "southwest" Or cmd = "south-west" Or cmd = "south west" Or cmd = "go sw" Or cmd = "go southwest" Or cmd = "go south-west" Or cmd = "go south west" Then
                GoDirection("southwest", ctx)
                _lastIt = 0
            ElseIf cmd = "up" Or cmd = "u" Then
                GoDirection("up", ctx)
                _lastIt = 0
            ElseIf cmd = "down" Or cmd = "d" Then
                GoDirection("down", ctx)
                _lastIt = 0
            ElseIf CmdStartsWith(thecommand, "go ") Then
                If _gameAslVersion >= 410 Then
                    _rooms(GetRoomID(_currentRoom, ctx)).Exits.ExecuteGo(thecommand, ctx)
                Else
                    D = GetEverythingAfter(thecommand, "go ")
                    If D = "out" Then
                        LeaveRoom(ctx)
                    ElseIf D = "north" Or D = "south" Or D = "east" Or D = "west" Or D = "up" Or D = "down" Then
                        GoDirection(D, ctx)
                    ElseIf BeginsWith(D, "to ") Then
                        P = GetEverythingAfter(D, "to ")
                        GoToPlace(P, ctx)
                    Else
                        PlayerErrorMessage(PlayerError.BadGo, ctx)
                    End If
                End If
                _lastIt = 0
            ElseIf CmdStartsWith(thecommand, "give ") Then
                G = GetEverythingAfter(thecommand, "give ")
                ExecGive(G, ctx)
            ElseIf CmdStartsWith(thecommand, "take ") Then
                T = GetEverythingAfter(thecommand, "take ")
                ExecTake(T, ctx)
            ElseIf CmdStartsWith(thecommand, "drop ") And _gameAslVersion >= 280 Then
                D = GetEverythingAfter(thecommand, "drop ")
                ExecDrop(D, ctx)
            ElseIf CmdStartsWith(thecommand, "get ") Then
                T = GetEverythingAfter(thecommand, "get ")
                ExecTake(T, ctx)
            ElseIf CmdStartsWith(thecommand, "pick up ") Then
                T = GetEverythingAfter(thecommand, "pick up ")
                ExecTake(T, ctx)
            ElseIf cmd = "pick it up" Or cmd = "pick them up" Or cmd = "pick this up" Or cmd = "pick that up" Or cmd = "pick these up" Or cmd = "pick those up" Or cmd = "pick him up" Or cmd = "pick her up" Then
                ExecTake(Mid(cmd, 6, InStr(7, cmd, " ") - 6), ctx)
            ElseIf CmdStartsWith(thecommand, "look ") Then
                ExecLook(thecommand, ctx)
            ElseIf CmdStartsWith(thecommand, "l ") Then
                ExecLook("look " & GetEverythingAfter(thecommand, "l "), ctx)
            ElseIf CmdStartsWith(thecommand, "examine ") And _gameAslVersion >= 280 Then
                ExecExamine(thecommand, ctx)
            ElseIf CmdStartsWith(thecommand, "x ") And _gameAslVersion >= 280 Then
                ExecExamine("examine " & GetEverythingAfter(thecommand, "x "), ctx)
            ElseIf cmd = "l" Or cmd = "look" Then
                ExecLook("look", ctx)
            ElseIf cmd = "x" Or cmd = "examine" Then
                ExecExamine("examine", ctx)
            ElseIf CmdStartsWith(thecommand, "use ") Then
                ExecUse(thecommand, ctx)
            ElseIf CmdStartsWith(thecommand, "open ") And _gameAslVersion >= 391 Then
                ExecOpenClose(thecommand, ctx)
            ElseIf CmdStartsWith(thecommand, "close ") And _gameAslVersion >= 391 Then
                ExecOpenClose(thecommand, ctx)
            ElseIf CmdStartsWith(thecommand, "put ") And _gameAslVersion >= 391 Then
                ExecAddRemove(thecommand, ctx)
            ElseIf CmdStartsWith(thecommand, "add ") And _gameAslVersion >= 391 Then
                ExecAddRemove(thecommand, ctx)
            ElseIf CmdStartsWith(thecommand, "remove ") And _gameAslVersion >= 391 Then
                ExecAddRemove(thecommand, ctx)
            ElseIf cmd = "save" Then
                m_player.RequestSave(Nothing)
            ElseIf cmd = "quit" Then
                GameFinished()
            ElseIf BeginsWith(cmd, "help") Then
                ShowHelp(ctx)
                EnteredHelpCommand = True
            ElseIf cmd = "about" Then
                ShowGameAbout(ctx)
            ElseIf cmd = "clear" Then
                DoClear()
            ElseIf cmd = "debug" Then
                ' TO DO: This is temporary, would be better to have a log viewer built in to Player
                For Each logEntry As String In _log
                    Print(logEntry, ctx)
                Next
            ElseIf cmd = "inventory" Or cmd = "inv" Or cmd = "i" Then
                InventoryPlace = "inventory"

                If _gameAslVersion >= 280 Then
                    For i = 1 To _numberObjs
                        If _objs(i).ContainerRoom = InventoryPlace And _objs(i).Exists And _objs(i).Visible Then
                            InvList = InvList & _objs(i).Prefix

                            If _objs(i).ObjectAlias = "" Then
                                InvList = InvList & "|b" & _objs(i).ObjectName & "|xb"
                            Else
                                InvList = InvList & "|b" & _objs(i).ObjectAlias & "|xb"
                            End If

                            If _objs(i).Suffix <> "" Then
                                InvList = InvList & " " & _objs(i).Suffix
                            End If

                            InvList = InvList & ", "
                        End If
                    Next i
                Else
                    For j = 1 To _numberItems
                        If _items(j).Got = True Then
                            InvList = InvList & _items(j).Name & ", "
                        End If
                    Next j
                End If
                If InvList <> "" Then

                    InvList = Left(InvList, Len(InvList) - 2)
                    InvList = UCase(Left(InvList, 1)) & Mid(InvList, 2)

                    CurPos = 1
                    Do
                        ThisComma = InStr(CurPos, InvList, ",")
                        If ThisComma <> 0 Then
                            LastComma = ThisComma
                            CurPos = ThisComma + 1
                        End If
                    Loop Until ThisComma = 0
                    If LastComma <> 0 Then InvList = Left(InvList, LastComma - 1) & " and" & Mid(InvList, LastComma + 1)
                    Print("You are carrying:|n" & InvList & ".", ctx)
                Else
                    Print("You are not carrying anything.", ctx)
                End If
            ElseIf CmdStartsWith(thecommand, "oops ") Then
                ExecOops(GetEverythingAfter(thecommand, "oops "), ctx)
            ElseIf CmdStartsWith(thecommand, "the ") Then
                ExecOops(GetEverythingAfter(thecommand, "the "), ctx)
            Else
                PlayerErrorMessage(PlayerError.BadCommand, ctx)
            End If
        End If

        If Not SkipAfterTurn Then
            ' Execute any "afterturn" script:
            GlobalOverride = False

            If RoomID <> 0 Then
                If _rooms(RoomID).AfterTurnScript <> "" Then
                    If BeginsWith(_rooms(RoomID).AfterTurnScript, "override") Then
                        ExecuteScript(GetEverythingAfter(_rooms(RoomID).AfterTurnScript, "override"), ctx)
                        GlobalOverride = True
                    Else
                        ExecuteScript(_rooms(RoomID).AfterTurnScript, ctx)
                    End If
                End If
            End If

            ' was set to NullThread here for some reason
            If _afterTurnScript <> "" And GlobalOverride = False Then ExecuteScript(_afterTurnScript, ctx)
        End If

        Print("", ctx)

        If Not DontSetIt Then
            ' Use "DontSetIt" when we don't want "it" etc. to refer to the object used in this turn.
            ' This is used for e.g. auto-remove object from container when taking.
            _lastIt = _thisTurnIt
            _lastItMode = _thisTurnItMode
        End If
        If _badCmdBefore = OldBadCmdBefore Then _badCmdBefore = ""

        Return True
    End Function

    Private Function CmdStartsWith(sCommand As String, sCheck As String) As Boolean
        ' When we are checking user input in ExecCommand, we check things like whether
        ' the player entered something beginning with "put ". We need to trim what the player entered
        ' though, otherwise they might just type "put " and then we would try disambiguating an object
        ' called "".

        Return BeginsWith(Trim(sCommand), sCheck)
    End Function

    Private Sub ExecGive(GiveString As String, ctx As Context)
        Dim characterblock As DefineBlock
        Dim ObjArticle As String
        Dim ToLoc As Integer
        Dim ItemToGive, CharToGive As String
        Dim ObjectType As Thing
        Dim ItemID As Integer
        Dim InventoryPlace As String
        Dim NotGotItem As Boolean
        Dim GiveScript As String = ""
        Dim FoundGiveScript, FoundGiveToObject As Boolean
        Dim GiveToObjectID, i As Integer
        Dim ObjGender As String
        Dim RealName, ItemCheck As String
        Dim GiveLine As Integer

        ToLoc = InStr(GiveString, " to ")
        If ToLoc = 0 Then
            ToLoc = InStr(GiveString, " the ")
            If ToLoc = 0 Then
                PlayerErrorMessage(PlayerError.BadGive, ctx)
                Exit Sub
            Else
                ItemToGive = Trim(Mid(GiveString, ToLoc + 4, Len(GiveString) - (ToLoc + 2)))
                CharToGive = Trim(Mid(GiveString, 1, ToLoc))
            End If
        Else
            ItemToGive = Trim(Mid(GiveString, 1, ToLoc))
            CharToGive = Trim(Mid(GiveString, ToLoc + 3, Len(GiveString) - (ToLoc + 2)))
        End If

        If _gameAslVersion >= 281 Then
            ObjectType = Thing.Object
        Else
            ObjectType = Thing.Character
        End If

        ' First see if player has "ItemToGive":
        If _gameAslVersion >= 280 Then
            InventoryPlace = "inventory"

            ItemID = Disambiguate(ItemToGive, InventoryPlace, ctx)

            If ItemID = -1 Then
                PlayerErrorMessage(PlayerError.NoItem, ctx)
                _badCmdBefore = "give"
                _badCmdAfter = "to " & CharToGive
                Exit Sub
            ElseIf ItemID = -2 Then
                Exit Sub
            Else
                ObjArticle = _objs(ItemID).Article
            End If
        Else
            ' ASL2:
            NotGotItem = True

            For i = 1 To _numberItems
                If LCase(_items(i).Name) = LCase(ItemToGive) Then
                    If _items(i).Got = False Then
                        NotGotItem = True
                        i = _numberItems
                    Else
                        NotGotItem = False
                    End If
                End If
            Next i

            If NotGotItem = True Then
                PlayerErrorMessage(PlayerError.NoItem, ctx)
                Exit Sub
            Else
                ObjArticle = _objs(ItemID).Article
            End If
        End If

        If _gameAslVersion >= 281 Then
            FoundGiveScript = False
            FoundGiveToObject = False

            GiveToObjectID = Disambiguate(CharToGive, _currentRoom, ctx)
            If GiveToObjectID > 0 Then
                FoundGiveToObject = True
            End If

            If Not FoundGiveToObject Then
                If GiveToObjectID <> -2 Then PlayerErrorMessage(PlayerError.BadCharacter, ctx)
                _badCmdBefore = "give " & ItemToGive & " to"
                Exit Sub
            End If

            'Find appropriate give script ****
            'now, for "give a to b", we have
            'ItemID=a and GiveToObjectID=b

            Dim o = _objs(GiveToObjectID)

            For i = 1 To o.NumberGiveData
                If o.GiveData(i).GiveType = GiveType.GiveSomethingTo And LCase(o.GiveData(i).GiveObject) = LCase(_objs(ItemID).ObjectName) Then
                    FoundGiveScript = True
                    GiveScript = o.GiveData(i).GiveScript
                    i = o.NumberGiveData
                End If
            Next i

            If Not FoundGiveScript Then
                'check a for give to <b>:

                Dim g = _objs(ItemID)

                For i = 1 To g.NumberGiveData
                    If g.GiveData(i).GiveType = GiveType.GiveToSomething And LCase(g.GiveData(i).GiveObject) = LCase(_objs(GiveToObjectID).ObjectName) Then
                        FoundGiveScript = True
                        GiveScript = g.GiveData(i).GiveScript
                        i = g.NumberGiveData
                    End If
                Next i
            End If

            If Not FoundGiveScript Then
                'check b for give anything:
                GiveScript = _objs(GiveToObjectID).GiveAnything
                If GiveScript <> "" Then
                    FoundGiveScript = True
                    SetStringContents("quest.give.object.name", _objs(ItemID).ObjectName, ctx)
                End If
            End If

            If Not FoundGiveScript Then
                'check a for give to anything:
                GiveScript = _objs(ItemID).GiveToAnything
                If GiveScript <> "" Then
                    FoundGiveScript = True
                    SetStringContents("quest.give.object.name", _objs(GiveToObjectID).ObjectName, ctx)
                End If
            End If

            If FoundGiveScript Then
                ExecuteScript(GiveScript, ctx, ItemID)
            Else
                SetStringContents("quest.error.charactername", _objs(GiveToObjectID).ObjectName, ctx)

                ObjGender = Trim(_objs(GiveToObjectID).Gender)
                ObjGender = UCase(Left(ObjGender, 1)) & Mid(ObjGender, 2)
                SetStringContents("quest.error.gender", ObjGender, ctx)

                SetStringContents("quest.error.article", ObjArticle, ctx)
                PlayerErrorMessage(PlayerError.ItemUnwanted, ctx)
            End If
        Else
            ' ASL2:
            characterblock = GetThingBlock(CharToGive, _currentRoom, ObjectType)

            If (characterblock.StartLine = 0 And characterblock.EndLine = 0) Or IsAvailable(CharToGive & "@" & _currentRoom, ObjectType, ctx) = False Then
                PlayerErrorMessage(PlayerError.BadCharacter, ctx)
                Exit Sub
            End If

            RealName = _chars(GetThingNumber(CharToGive, _currentRoom, ObjectType)).ObjectName

            ' now, see if there's a give statement for this item in
            ' this characters definition:

            GiveLine = 0
            For i = characterblock.StartLine + 1 To characterblock.EndLine - 1
                If BeginsWith(_lines(i), "give") Then
                    ItemCheck = RetrieveParameter(_lines(i), ctx)
                    If LCase(ItemCheck) = LCase(ItemToGive) Then
                        GiveLine = i
                    End If
                End If
            Next i

            If GiveLine = 0 Then
                If ObjArticle = "" Then ObjArticle = "it"
                SetStringContents("quest.error.charactername", RealName, ctx)
                SetStringContents("quest.error.gender", Trim(GetGender(CharToGive, True, ctx)), ctx)
                SetStringContents("quest.error.article", ObjArticle, ctx)
                PlayerErrorMessage(PlayerError.ItemUnwanted, ctx)
                Exit Sub
            End If

            ' now, execute the statement on GiveLine
            ExecuteScript(GetSecondChunk(_lines(GiveLine)), ctx)
        End If

    End Sub

    Private Sub ExecLook(LookLine As String, ctx As Context)
        Dim AtPos As Integer
        Dim LookStuff, LookItem, LookText As String

        Dim InventoryPlace As String
        Dim FoundObject As Boolean
        Dim ObjID As Integer
        If Trim(LookLine) = "look" Then
            ShowRoomInfo((_currentRoom), ctx)
        Else
            If _gameAslVersion < 391 Then
                AtPos = InStr(LookLine, " at ")

                If AtPos = 0 Then
                    LookItem = GetEverythingAfter(LookLine, "look ")
                Else
                    LookItem = Trim(Mid(LookLine, AtPos + 4))
                End If
            Else
                If BeginsWith(LookLine, "look at ") Then
                    LookItem = GetEverythingAfter(LookLine, "look at ")
                ElseIf BeginsWith(LookLine, "look in ") Then
                    LookItem = GetEverythingAfter(LookLine, "look in ")
                ElseIf BeginsWith(LookLine, "look on ") Then
                    LookItem = GetEverythingAfter(LookLine, "look on ")
                ElseIf BeginsWith(LookLine, "look inside ") Then
                    LookItem = GetEverythingAfter(LookLine, "look inside ")
                Else
                    LookItem = GetEverythingAfter(LookLine, "look ")
                End If
            End If

            If _gameAslVersion >= 280 Then
                FoundObject = False

                InventoryPlace = "inventory"

                ObjID = Disambiguate(LookItem, InventoryPlace & ";" & _currentRoom, ctx)
                If ObjID > 0 Then
                    FoundObject = True
                End If

                If Not FoundObject Then
                    If ObjID <> -2 Then PlayerErrorMessage(PlayerError.BadThing, ctx)
                    _badCmdBefore = "look at"
                    Exit Sub
                End If

                DoLook(ObjID, ctx)
            Else
                If BeginsWith(LookItem, "the ") Then
                    LookItem = GetEverythingAfter(LookItem, "the ")
                End If

                LookLine = RetrLine("object", LookItem, "look", ctx)

                If LookLine <> "<unfound>" Then
                    'Check for availability
                    If IsAvailable(LookItem, Thing.Object, ctx) = False Then
                        LookLine = "<unfound>"
                    End If
                End If

                If LookLine = "<unfound>" Then
                    LookLine = RetrLine("character", LookItem, "look", ctx)

                    If LookLine <> "<unfound>" Then
                        If IsAvailable(LookItem, Thing.Character, ctx) = False Then
                            LookLine = "<unfound>"
                        End If
                    End If

                    If LookLine = "<unfound>" Then
                        PlayerErrorMessage(PlayerError.BadThing, ctx)
                        Exit Sub
                    ElseIf LookLine = "<undefined>" Then
                        PlayerErrorMessage(PlayerError.DefaultLook, ctx)
                        Exit Sub
                    End If
                ElseIf LookLine = "<undefined>" Then
                    PlayerErrorMessage(PlayerError.DefaultLook, ctx)
                    Exit Sub
                End If

                LookStuff = Trim(GetEverythingAfter(Trim(LookLine), "look "))
                If Left(LookStuff, 1) = "<" Then
                    LookText = RetrieveParameter(LookLine, ctx)
                    Print(LookText, ctx)
                Else
                    ExecuteScript(LookStuff, ctx, ObjID)
                End If
            End If
        End If

    End Sub

    Private Sub ExecSpeak(c As String, ctx As Context)
        Dim i As Integer
        Dim l, s As String

        If BeginsWith(c, "the ") Then
            c = GetEverythingAfter(c, "the ")
        End If

        Dim ObjectName As String
        ObjectName = c

        ' if the "speak" parameter of the character c$'s definition
        ' is just a parameter, say it - otherwise execute it as
        ' a script.

        Dim ObjectType As Thing

        Dim SpeakLine As String = ""
        Dim InventoryPlace As String
        Dim FoundSpeak As Boolean
        Dim SpeakText As String
        Dim FoundObject As Boolean
        Dim ObjID As Integer
        If _gameAslVersion >= 281 Then

            FoundObject = False

            InventoryPlace = "inventory"

            ObjID = Disambiguate(ObjectName, InventoryPlace & ";" & _currentRoom, ctx)
            If ObjID > 0 Then
                FoundObject = True
            End If

            If Not FoundObject Then
                If ObjID <> -2 Then PlayerErrorMessage(PlayerError.BadThing, ctx)
                _badCmdBefore = "speak to"
                Exit Sub
            End If

            FoundSpeak = False

            ' First look for action, then look
            ' for property, then check define
            ' section:

            Dim o = _objs(ObjID)

            For i = 1 To o.NumberActions
                If o.Actions(i).ActionName = "speak" Then
                    SpeakLine = "speak " & o.Actions(i).Script
                    FoundSpeak = True
                    i = o.NumberActions
                End If
            Next i

            If Not FoundSpeak Then
                For i = 1 To o.NumberProperties
                    If o.Properties(i).PropertyName = "speak" Then
                        SpeakLine = "speak <" & o.Properties(i).PropertyValue & ">"
                        FoundSpeak = True
                        i = o.NumberProperties
                    End If
                Next i
            End If

            If _gameAslVersion < 311 Then
                ' For some reason ASL3 < 311 looks for a "look" tag rather than
                ' having had this set up at initialisation.
                If Not FoundSpeak Then
                    For i = o.DefinitionSectionStart To o.DefinitionSectionEnd
                        If BeginsWith(_lines(i), "speak ") Then
                            SpeakLine = _lines(i)
                            FoundSpeak = True
                        End If
                    Next i
                End If
            End If

            If Not FoundSpeak Then
                SetStringContents("quest.error.gender", UCase(Left(_objs(ObjID).Gender, 1)) & Mid(_objs(ObjID).Gender, 2), ctx)
                PlayerErrorMessage(PlayerError.DefaultSpeak, ctx)
                Exit Sub
            End If

            SpeakLine = GetEverythingAfter(SpeakLine, "speak ")

            If BeginsWith(SpeakLine, "<") Then
                SpeakText = RetrieveParameter(SpeakLine, ctx)
                If _gameAslVersion >= 350 Then
                    Print(SpeakText, ctx)
                Else
                    Print(Chr(34) & SpeakText & Chr(34), ctx)
                End If
            Else
                ExecuteScript(SpeakLine, ctx, ObjID)
            End If

        Else
            l = RetrLine("character", c, "speak", ctx)
            ObjectType = Thing.Character

            s = Trim(GetEverythingAfter(Trim(l), "speak "))

            If l <> "<unfound>" And l <> "<undefined>" Then
                ' Character exists; but is it available??
                If IsAvailable(c & "@" & _currentRoom, ObjectType, ctx) = False Then
                    l = "<undefined>"
                End If
            End If

            If l = "<undefined>" Then
                PlayerErrorMessage(PlayerError.BadCharacter, ctx)
            ElseIf l = "<unfound>" Then
                SetStringContents("quest.error.gender", Trim(GetGender(c, True, ctx)), ctx)
                SetStringContents("quest.error.charactername", c, ctx)
                PlayerErrorMessage(PlayerError.DefaultSpeak, ctx)
            ElseIf BeginsWith(s, "<") Then
                s = RetrieveParameter(l, ctx)
                Print(Chr(34) & s & Chr(34), ctx)
            Else
                ExecuteScript(s, ctx)
            End If
        End If

    End Sub

    Private Sub ExecTake(TakeItem As String, ctx As Context)
        Dim i As Integer
        Dim FoundItem, FoundTake As Boolean
        Dim OriginalTakeItem As String
        Dim ObjID As Integer
        Dim ParentID As Integer
        Dim ParentDisplayName As String
        Dim ObjectIsInContainer As Boolean
        Dim ScriptLine As String
        Dim ContainerError As String = ""

        FoundItem = True
        FoundTake = False
        OriginalTakeItem = TakeItem

        ObjID = Disambiguate(TakeItem, _currentRoom, ctx)

        If ObjID < 0 Then
            FoundItem = False
        Else
            FoundItem = True
        End If

        If FoundItem = False Then
            If ObjID <> -2 Then
                If _gameAslVersion >= 410 Then
                    ObjID = Disambiguate(TakeItem, "inventory", ctx)
                    If ObjID >= 0 Then
                        ' Player already has this item
                        PlayerErrorMessage(PlayerError.AlreadyTaken, ctx)
                    Else
                        PlayerErrorMessage(PlayerError.BadThing, ctx)
                    End If
                ElseIf _gameAslVersion >= 391 Then
                    PlayerErrorMessage(PlayerError.BadThing, ctx)
                Else
                    PlayerErrorMessage(PlayerError.BadItem, ctx)
                End If
            End If
            _badCmdBefore = "take"
            Exit Sub
        Else
            SetStringContents("quest.error.article", _objs(ObjID).Article, ctx)
        End If

        ObjectIsInContainer = False

        If _gameAslVersion >= 391 Then

            If Not PlayerCanAccessObject(ObjID, ParentID, ContainerError) Then
                PlayerErrorMessage_ExtendInfo(PlayerError.BadTake, ctx, ContainerError)
                Exit Sub
            End If

        End If

        If _gameAslVersion >= 280 Then
            Dim t = _objs(ObjID).Take

            If ObjectIsInContainer And (t.Type = TextActionType.Default Or t.Type = TextActionType.Text) Then
                ' So, we want to take an object that's in a container or surface. So first
                ' we have to remove the object from that container.

                If _objs(ParentID).ObjectAlias <> "" Then
                    ParentDisplayName = _objs(ParentID).ObjectAlias
                Else
                    ParentDisplayName = _objs(ParentID).ObjectName
                End If

                Print("(first removing " & _objs(ObjID).Article & " from " & ParentDisplayName & ")", ctx)

                ' Try to remove the object
                ctx.AllowRealNamesInCommand = True
                ExecCommand("remove " & _objs(ObjID).ObjectName, ctx, False, , True)

                If GetObjectProperty("parent", ObjID, False, False) <> "" Then
                    ' removing the object failed
                    Exit Sub
                End If
            End If

            If t.Type = TextActionType.Default Then
                PlayerErrorMessage(PlayerError.DefaultTake, ctx)
                PlayerItem(TakeItem, True, ctx, ObjID)
            ElseIf t.Type = TextActionType.Text Then
                Print(t.Data, ctx)
                PlayerItem(TakeItem, True, ctx, ObjID)
            ElseIf t.Type = TextActionType.Script Then
                ExecuteScript(t.Data, ctx, ObjID)
            Else
                PlayerErrorMessage(PlayerError.BadTake, ctx)
            End If
        Else

            ' find 'take' line
            For i = _objs(ObjID).DefinitionSectionStart + 1 To _objs(ObjID).DefinitionSectionEnd - 1
                If BeginsWith(_lines(i), "take") Then
                    ScriptLine = Trim(GetEverythingAfter(Trim(_lines(i)), "take"))
                    ExecuteScript(ScriptLine, ctx, ObjID)
                    FoundTake = True
                    i = _objs(ObjID).DefinitionSectionEnd
                End If
            Next i

            If FoundTake = False Then
                PlayerErrorMessage(PlayerError.BadTake, ctx)
            End If
        End If

    End Sub

    Private Sub ExecUse(tuseline As String, ctx As Context)
        Dim OnWithPos, i, EndOnWith As Integer
        Dim UseLine As String
        Dim UseDeclareLine As String = "", UseOn, UseItem, ScriptLine As String

        UseLine = Trim(GetEverythingAfter(tuseline, "use "))

        Dim RoomID As Integer
        RoomID = GetRoomID(_currentRoom, ctx)

        OnWithPos = InStr(UseLine, " on ")
        If OnWithPos = 0 Then
            OnWithPos = InStr(UseLine, " with ")
            EndOnWith = OnWithPos + 4
        Else
            EndOnWith = OnWithPos + 2
        End If


        If OnWithPos <> 0 Then
            UseOn = Trim(Right(UseLine, Len(UseLine) - EndOnWith))
            UseItem = Trim(Left(UseLine, OnWithPos - 1))
        Else
            UseOn = ""
            UseItem = UseLine
        End If

        ' see if player has this item:

        Dim FoundItem As Boolean
        Dim ItemID As Integer
        Dim InventoryPlace As String = ""
        Dim NotGotItem As Boolean
        If _gameAslVersion >= 280 Then
            InventoryPlace = "inventory"

            FoundItem = False

            ItemID = Disambiguate(UseItem, InventoryPlace, ctx)
            If ItemID > 0 Then FoundItem = True

            If Not FoundItem Then
                If ItemID <> -2 Then PlayerErrorMessage(PlayerError.NoItem, ctx)
                If UseOn = "" Then
                    _badCmdBefore = "use"
                Else
                    _badCmdBefore = "use"
                    _badCmdAfter = "on " & UseOn
                End If
                Exit Sub
            End If
        Else
            NotGotItem = True

            For i = 1 To _numberItems
                If LCase(_items(i).Name) = LCase(UseItem) Then
                    If _items(i).Got = False Then
                        NotGotItem = True
                        i = _numberItems
                    Else
                        NotGotItem = False
                    End If
                End If
            Next i

            If NotGotItem = True Then
                PlayerErrorMessage(PlayerError.NoItem, ctx)
                Exit Sub
            End If
        End If

        Dim UseScript As String = ""
        Dim FoundUseScript As Boolean
        Dim FoundUseOnObject As Boolean
        Dim UseOnObjectID As Integer
        Dim Found As Boolean
        If _gameAslVersion >= 280 Then
            FoundUseScript = False

            If UseOn = "" Then
                If _gameAslVersion < 410 Then
                    Dim r = _rooms(RoomID)
                    For i = 1 To r.NumberUse
                        If LCase(_objs(ItemID).ObjectName) = LCase(r.Use(i).Text) Then
                            FoundUseScript = True
                            UseScript = r.Use(i).Script
                            i = r.NumberUse
                        End If
                    Next i
                End If

                If Not FoundUseScript Then
                    UseScript = _objs(ItemID).Use
                    If UseScript <> "" Then FoundUseScript = True
                End If
            Else
                FoundUseOnObject = False

                UseOnObjectID = Disambiguate(UseOn, _currentRoom, ctx)
                If UseOnObjectID > 0 Then
                    FoundUseOnObject = True
                Else
                    UseOnObjectID = Disambiguate(UseOn, InventoryPlace, ctx)
                    If UseOnObjectID > 0 Then
                        FoundUseOnObject = True
                    End If
                End If

                If Not FoundUseOnObject Then
                    If UseOnObjectID <> -2 Then PlayerErrorMessage(PlayerError.BadThing, ctx)
                    _badCmdBefore = "use " & UseItem & " on"
                    Exit Sub
                End If

                'now, for "use a on b", we have
                'ItemID=a and UseOnObjectID=b

                'first check b for use <a>:

                Dim o = _objs(UseOnObjectID)

                For i = 1 To o.NumberUseData
                    If o.UseData(i).UseType = UseType.UseSomethingOn And LCase(o.UseData(i).UseObject) = LCase(_objs(ItemID).ObjectName) Then
                        FoundUseScript = True
                        UseScript = o.UseData(i).UseScript
                        i = o.NumberUseData
                    End If
                Next i

                If Not FoundUseScript Then
                    'check a for use on <b>:
                    Dim u = _objs(ItemID)
                    For i = 1 To u.NumberUseData
                        If u.UseData(i).UseType = UseType.UseOnSomething And LCase(u.UseData(i).UseObject) = LCase(_objs(UseOnObjectID).ObjectName) Then
                            FoundUseScript = True
                            UseScript = u.UseData(i).UseScript
                            i = u.NumberUseData
                        End If
                    Next i
                End If

                If Not FoundUseScript Then
                    'check b for use anything:
                    UseScript = _objs(UseOnObjectID).UseAnything
                    If UseScript <> "" Then
                        FoundUseScript = True
                        SetStringContents("quest.use.object.name", _objs(ItemID).ObjectName, ctx)
                    End If
                End If

                If Not FoundUseScript Then
                    'check a for use on anything:
                    UseScript = _objs(ItemID).UseOnAnything
                    If UseScript <> "" Then
                        FoundUseScript = True
                        SetStringContents("quest.use.object.name", _objs(UseOnObjectID).ObjectName, ctx)
                    End If
                End If
            End If

            If FoundUseScript Then
                ExecuteScript(UseScript, ctx, ItemID)
            Else
                PlayerErrorMessage(PlayerError.DefaultUse, ctx)
            End If
        Else
            If UseOn <> "" Then
                UseDeclareLine = RetrLineParam("object", UseOn, "use", UseItem, ctx)
            Else
                Found = False
                For i = 1 To _rooms(RoomID).NumberUse
                    If LCase(_rooms(RoomID).Use(i).Text) = LCase(UseItem) Then
                        UseDeclareLine = "use <> " & _rooms(RoomID).Use(i).Script
                        Found = True
                        i = _rooms(RoomID).NumberUse
                    End If
                Next i

                If Not Found Then
                    UseDeclareLine = FindLine(GetDefineBlock("game"), "use", UseItem)
                End If

                If Not Found And UseDeclareLine = "" Then
                    PlayerErrorMessage(PlayerError.DefaultUse, ctx)
                    Exit Sub
                End If
            End If

            If UseDeclareLine <> "<unfound>" And UseDeclareLine <> "<undefined>" And UseOn <> "" Then
                'Check for object availablity
                If IsAvailable(UseOn, Thing.Object, ctx) = False Then
                    UseDeclareLine = "<undefined>"
                End If
            End If

            If UseDeclareLine = "<undefined>" Then
                UseDeclareLine = RetrLineParam("character", UseOn, "use", UseItem, ctx)

                If UseDeclareLine <> "<undefined>" Then
                    'Check for character availability
                    If IsAvailable(UseOn, Thing.Character, ctx) = False Then
                        UseDeclareLine = "<undefined>"
                    End If
                End If

                If UseDeclareLine = "<undefined>" Then
                    PlayerErrorMessage(PlayerError.BadThing, ctx)
                    Exit Sub
                ElseIf UseDeclareLine = "<unfound>" Then
                    PlayerErrorMessage(PlayerError.DefaultUse, ctx)
                    Exit Sub
                End If
            ElseIf UseDeclareLine = "<unfound>" Then
                PlayerErrorMessage(PlayerError.DefaultUse, ctx)
                Exit Sub
            End If

            ScriptLine = Right(UseDeclareLine, Len(UseDeclareLine) - InStr(UseDeclareLine, ">"))
            ExecuteScript(ScriptLine, ctx)
        End If

    End Sub

    Private Sub ObjectActionUpdate(ObjID As Integer, ActionName As String, ActionScript As String, Optional NoUpdate As Boolean = False)
        Dim ObjectName As String
        Dim SP, EP As Integer
        ActionName = LCase(ActionName)

        If Not NoUpdate Then
            If ActionName = "take" Then
                _objs(ObjID).Take.Data = ActionScript
                _objs(ObjID).Take.Type = TextActionType.Script
            ElseIf ActionName = "use" Then
                AddToUseInfo(ObjID, ActionScript)
            ElseIf ActionName = "gain" Then
                _objs(ObjID).GainScript = ActionScript
            ElseIf ActionName = "lose" Then
                _objs(ObjID).LoseScript = ActionScript
            ElseIf BeginsWith(ActionName, "use ") Then
                ActionName = GetEverythingAfter(ActionName, "use ")
                If InStr(ActionName, "'") > 0 Then
                    SP = InStr(ActionName, "'")
                    EP = InStr(SP + 1, ActionName, "'")
                    If EP = 0 Then
                        LogASLError("Missing ' in 'action <use " & ActionName & "> " & ReportErrorLine(ActionScript))
                        Exit Sub
                    End If

                    ObjectName = Mid(ActionName, SP + 1, EP - SP - 1)

                    AddToUseInfo(ObjID, Trim(Left(ActionName, SP - 1)) & " <" & ObjectName & "> " & ActionScript)
                Else
                    AddToUseInfo(ObjID, ActionName & " " & ActionScript)
                End If
            ElseIf BeginsWith(ActionName, "give ") Then
                ActionName = GetEverythingAfter(ActionName, "give ")
                If InStr(ActionName, "'") > 0 Then

                    SP = InStr(ActionName, "'")
                    EP = InStr(SP + 1, ActionName, "'")
                    If EP = 0 Then
                        LogASLError("Missing ' in 'action <give " & ActionName & "> " & ReportErrorLine(ActionScript))
                        Exit Sub
                    End If

                    ObjectName = Mid(ActionName, SP + 1, EP - SP - 1)

                    AddToGiveInfo(ObjID, Trim(Left(ActionName, SP - 1)) & " <" & ObjectName & "> " & ActionScript)
                Else
                    AddToGiveInfo(ObjID, ActionName & " " & ActionScript)
                End If
            End If
        End If

        If _gameFullyLoaded Then
            AddToOOChangeLog(ChangeLog.eAppliesToType.atObject, _objs(ObjID).ObjectName, ActionName, "action <" & ActionName & "> " & ActionScript)
        End If

    End Sub

    Private Sub ExecuteIf(ScriptLine As String, ctx As Context)
        Dim IfLine, Conditions, ObscuredLine As String
        Dim ElsePos, ThenPos, ThenEndPos As Integer
        Dim ThenScript As String, ElseScript As String = ""

        IfLine = Trim(GetEverythingAfter(Trim(ScriptLine), "if "))
        ObscuredLine = ObliterateParameters(IfLine)

        ThenPos = InStr(ObscuredLine, "then")

        Dim ErrMsg As String
        If ThenPos = 0 Then
            ErrMsg = "Expected 'then' missing from script statement '" & ReportErrorLine(ScriptLine) & "' - statement bypassed."
            LogASLError(ErrMsg, LogType.WarningError)
            Exit Sub
        End If

        Conditions = Trim(Left(IfLine, ThenPos - 1))

        ThenPos = ThenPos + 4
        ElsePos = InStr(ObscuredLine, "else")

        If ElsePos = 0 Then
            ThenEndPos = Len(ObscuredLine) + 1
        Else
            ThenEndPos = ElsePos - 1
        End If

        ThenScript = Trim(Mid(IfLine, ThenPos, ThenEndPos - ThenPos))

        If ElsePos <> 0 Then
            ElseScript = Trim(Right(IfLine, Len(IfLine) - (ThenEndPos + 4)))
        End If

        ' Remove braces from around "then" and "else" script
        ' commands, if present
        If Left(ThenScript, 1) = "{" And Right(ThenScript, 1) = "}" Then
            ThenScript = Mid(ThenScript, 2, Len(ThenScript) - 2)
        End If
        If Left(ElseScript, 1) = "{" And Right(ElseScript, 1) = "}" Then
            ElseScript = Mid(ElseScript, 2, Len(ElseScript) - 2)
        End If

        If ExecuteConditions(Conditions, ctx) Then
            ExecuteScript((ThenScript), ctx)
        Else
            If ElsePos <> 0 Then ExecuteScript((ElseScript), ctx)
        End If

    End Sub

    Private Sub ExecuteScript(ScriptLine As String, ctx As Context, Optional NewCallingObjectID As Integer = 0)
        Try
            If Trim(ScriptLine) = "" Then Exit Sub
            If m_gameFinished Then Exit Sub

            Dim CurPos As Integer
            Dim bFinLoop As Boolean
            Dim CRLFPos As Integer
            Dim CurScriptLine As String
            If InStr(ScriptLine, vbCrLf) > 0 Then
                CurPos = 1
                bFinLoop = False
                Do
                    CRLFPos = InStr(CurPos, ScriptLine, vbCrLf)
                    If CRLFPos = 0 Then
                        bFinLoop = True
                        CRLFPos = Len(ScriptLine) + 1
                    End If

                    CurScriptLine = Trim(Mid(ScriptLine, CurPos, CRLFPos - CurPos))
                    If CurScriptLine <> vbCrLf Then
                        ExecuteScript(CurScriptLine, ctx)
                    End If
                    CurPos = CRLFPos + 2
                Loop Until bFinLoop
                Exit Sub
            End If

            If NewCallingObjectID <> 0 Then
                ctx.CallingObjectID = NewCallingObjectID
            End If

            If BeginsWith(ScriptLine, "if ") Then
                ExecuteIf(ScriptLine, ctx)
            ElseIf BeginsWith(ScriptLine, "select case ") Then
                ExecuteSelectCase(ScriptLine, ctx)
            ElseIf BeginsWith(ScriptLine, "choose ") Then
                ExecuteChoose(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "set ") Then
                ExecuteSet(GetEverythingAfter(ScriptLine, "set "), ctx)
            ElseIf BeginsWith(ScriptLine, "inc ") Or BeginsWith(ScriptLine, "dec ") Then
                ExecuteIncDec(ScriptLine, ctx)
            ElseIf BeginsWith(ScriptLine, "say ") Then
                Print(Chr(34) & RetrieveParameter(ScriptLine, ctx) & Chr(34), ctx)
            ElseIf BeginsWith(ScriptLine, "do ") Then
                ExecuteDo(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "doaction ") Then
                ExecuteDoAction(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "give ") Then
                PlayerItem(RetrieveParameter(ScriptLine, ctx), True, ctx)
            ElseIf BeginsWith(ScriptLine, "lose ") Or BeginsWith(ScriptLine, "drop ") Then
                PlayerItem(RetrieveParameter(ScriptLine, ctx), False, ctx)
            ElseIf BeginsWith(ScriptLine, "msg nospeak ") Then
                Print(RetrieveParameter(ScriptLine, ctx), ctx, , True)
            ElseIf BeginsWith(ScriptLine, "msg ") Then
                Print(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "speak ") Then
                Speak(RetrieveParameter(ScriptLine, ctx))
            ElseIf BeginsWith(ScriptLine, "helpmsg ") Then
                Print(RetrieveParameter(ScriptLine, ctx), ctx, "help")
            ElseIf Trim(LCase(ScriptLine)) = "helpclose" Then
                ' This command does nothing in the Quest 5 player, as there is no separate help window
            ElseIf BeginsWith(ScriptLine, "goto ") Then
                PlayGame(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "playerwin") Then
                FinishGame(StopType.Win, ctx)
            ElseIf BeginsWith(ScriptLine, "playerlose") Then
                FinishGame(StopType.Lose, ctx)
            ElseIf Trim(LCase(ScriptLine)) = "stop" Then
                FinishGame(StopType.Null, ctx)
            ElseIf BeginsWith(ScriptLine, "playwav ") Then
                PlayWav(RetrieveParameter(ScriptLine, ctx))
            ElseIf BeginsWith(ScriptLine, "playmidi ") Then
                PlayMedia(RetrieveParameter(ScriptLine, ctx))
            ElseIf BeginsWith(ScriptLine, "playmp3 ") Then
                PlayMedia(RetrieveParameter(ScriptLine, ctx))
            ElseIf Trim(LCase(ScriptLine)) = "picture close" Then
                ' This command does nothing in the Quest 5 player, as there is no separate picture window
            ElseIf (_gameAslVersion >= 390 And BeginsWith(ScriptLine, "picture popup ")) Or (_gameAslVersion >= 282 And _gameAslVersion < 390 And BeginsWith(ScriptLine, "picture ")) Or (_gameAslVersion < 282 And BeginsWith(ScriptLine, "show ")) Then
                ShowPicture(RetrieveParameter(ScriptLine, ctx))
            ElseIf (_gameAslVersion >= 390 And BeginsWith(ScriptLine, "picture ")) Then
                ShowPictureInText(RetrieveParameter(ScriptLine, ctx))
            ElseIf BeginsWith(ScriptLine, "animate persist ") Then
                ShowPicture(RetrieveParameter(ScriptLine, ctx))
            ElseIf BeginsWith(ScriptLine, "animate ") Then
                ShowPicture(RetrieveParameter(ScriptLine, ctx))
            ElseIf BeginsWith(ScriptLine, "extract ") Then
                ExtractFile(RetrieveParameter(ScriptLine, ctx))
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "hideobject ") Then
                SetAvailability(RetrieveParameter(ScriptLine, ctx), False, ctx)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "showobject ") Then
                SetAvailability(RetrieveParameter(ScriptLine, ctx), True, ctx)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "moveobject ") Then
                ExecMoveThing(RetrieveParameter(ScriptLine, ctx), Thing.Object, ctx)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "hidechar ") Then
                SetAvailability(RetrieveParameter(ScriptLine, ctx), False, ctx, Thing.Character)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "showchar ") Then
                SetAvailability(RetrieveParameter(ScriptLine, ctx), True, ctx, Thing.Character)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "movechar ") Then
                ExecMoveThing(RetrieveParameter(ScriptLine, ctx), Thing.Character, ctx)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "revealobject ") Then
                SetVisibility(RetrieveParameter(ScriptLine, ctx), Thing.Object, True, ctx)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "concealobject ") Then
                SetVisibility(RetrieveParameter(ScriptLine, ctx), Thing.Object, False, ctx)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "revealchar ") Then
                SetVisibility(RetrieveParameter(ScriptLine, ctx), Thing.Character, True, ctx)
            ElseIf _gameAslVersion < 281 And BeginsWith(ScriptLine, "concealchar ") Then
                SetVisibility(RetrieveParameter(ScriptLine, ctx), Thing.Character, False, ctx)
            ElseIf _gameAslVersion >= 281 And BeginsWith(ScriptLine, "hide ") Then
                SetAvailability(RetrieveParameter(ScriptLine, ctx), False, ctx)
            ElseIf _gameAslVersion >= 281 And BeginsWith(ScriptLine, "show ") Then
                SetAvailability(RetrieveParameter(ScriptLine, ctx), True, ctx)
            ElseIf _gameAslVersion >= 281 And BeginsWith(ScriptLine, "move ") Then
                ExecMoveThing(RetrieveParameter(ScriptLine, ctx), Thing.Object, ctx)
            ElseIf _gameAslVersion >= 281 And BeginsWith(ScriptLine, "reveal ") Then
                SetVisibility(RetrieveParameter(ScriptLine, ctx), Thing.Object, True, ctx)
            ElseIf _gameAslVersion >= 281 And BeginsWith(ScriptLine, "conceal ") Then
                SetVisibility(RetrieveParameter(ScriptLine, ctx), Thing.Object, False, ctx)
            ElseIf _gameAslVersion >= 391 And BeginsWith(ScriptLine, "open ") Then
                SetOpenClose(RetrieveParameter(ScriptLine, ctx), True, ctx)
            ElseIf _gameAslVersion >= 391 And BeginsWith(ScriptLine, "close ") Then
                SetOpenClose(RetrieveParameter(ScriptLine, ctx), False, ctx)
            ElseIf _gameAslVersion >= 391 And BeginsWith(ScriptLine, "add ") Then
                ExecAddRemoveScript(RetrieveParameter(ScriptLine, ctx), True, ctx)
            ElseIf _gameAslVersion >= 391 And BeginsWith(ScriptLine, "remove ") Then
                ExecAddRemoveScript(RetrieveParameter(ScriptLine, ctx), False, ctx)
            ElseIf BeginsWith(ScriptLine, "clone ") Then
                ExecClone(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "exec ") Then
                ExecExec(ScriptLine, ctx)
            ElseIf BeginsWith(ScriptLine, "setstring ") Then
                ExecSetString(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "setvar ") Then
                ExecSetVar(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "for ") Then
                ExecFor(GetEverythingAfter(ScriptLine, "for "), ctx)
            ElseIf BeginsWith(ScriptLine, "property ") Then
                ExecProperty(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "type ") Then
                ExecType(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "action ") Then
                ExecuteAction(GetEverythingAfter(ScriptLine, "action "), ctx)
            ElseIf BeginsWith(ScriptLine, "flag ") Then
                ExecuteFlag(GetEverythingAfter(ScriptLine, "flag "), ctx)
            ElseIf BeginsWith(ScriptLine, "create ") Then
                ExecuteCreate(GetEverythingAfter(ScriptLine, "create "), ctx)
            ElseIf BeginsWith(ScriptLine, "destroy exit ") Then
                DestroyExit(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "repeat ") Then
                ExecuteRepeat(GetEverythingAfter(ScriptLine, "repeat "), ctx)
            ElseIf BeginsWith(ScriptLine, "enter ") Then
                ExecuteEnter(ScriptLine, ctx)
            ElseIf BeginsWith(ScriptLine, "displaytext ") Then
                DisplayTextSection(RetrieveParameter(ScriptLine, ctx), ctx, "normal")
            ElseIf BeginsWith(ScriptLine, "helpdisplaytext ") Then
                DisplayTextSection(RetrieveParameter(ScriptLine, ctx), ctx, "help")
            ElseIf BeginsWith(ScriptLine, "font ") Then
                SetFont(RetrieveParameter(ScriptLine, ctx), ctx)
            ElseIf BeginsWith(ScriptLine, "pause ") Then
                Pause(CInt(RetrieveParameter(ScriptLine, ctx)))
            ElseIf Trim(LCase(ScriptLine)) = "clear" Then
                DoClear()
            ElseIf Trim(LCase(ScriptLine)) = "helpclear" Then
                ' This command does nothing in the Quest 5 player, as there is no separate help window
            ElseIf BeginsWith(ScriptLine, "background ") Then
                SetBackground(RetrieveParameter(ScriptLine, ctx))
            ElseIf BeginsWith(ScriptLine, "foreground ") Then
                SetForeground(RetrieveParameter(ScriptLine, ctx))
            ElseIf Trim(LCase(ScriptLine)) = "nointro" Then
                _autoIntro = False
            ElseIf BeginsWith(ScriptLine, "debug ") Then
                LogASLError(RetrieveParameter(ScriptLine, ctx), LogType.Misc)
            ElseIf BeginsWith(ScriptLine, "mailto ") Then
                Dim emailAddress As String = RetrieveParameter(ScriptLine, ctx)
                RaiseEvent PrintText("<a target=""_blank"" href=""mailto:" + emailAddress + """>" + emailAddress + "</a>")
            ElseIf BeginsWith(ScriptLine, "shell ") And _gameAslVersion < 410 Then
                LogASLError("'shell' is not supported in this version of Quest", LogType.WarningError)
            ElseIf BeginsWith(ScriptLine, "shellexe ") And _gameAslVersion < 410 Then
                LogASLError("'shellexe' is not supported in this version of Quest", LogType.WarningError)
            ElseIf BeginsWith(ScriptLine, "wait") Then
                ExecuteWait(Trim(GetEverythingAfter(Trim(ScriptLine), "wait")), ctx)
            ElseIf BeginsWith(ScriptLine, "timeron ") Then
                SetTimerState(RetrieveParameter(ScriptLine, ctx), True)
            ElseIf BeginsWith(ScriptLine, "timeroff ") Then
                SetTimerState(RetrieveParameter(ScriptLine, ctx), False)
            ElseIf Trim(LCase(ScriptLine)) = "outputon" Then
                _outPutOn = True
                UpdateObjectList(ctx)
                UpdateItems(ctx)
            ElseIf Trim(LCase(ScriptLine)) = "outputoff" Then
                _outPutOn = False
            ElseIf Trim(LCase(ScriptLine)) = "panes off" Then
                m_player.SetPanesVisible("off")
            ElseIf Trim(LCase(ScriptLine)) = "panes on" Then
                m_player.SetPanesVisible("on")
            ElseIf BeginsWith(ScriptLine, "lock ") Then
                ExecuteLock(RetrieveParameter(ScriptLine, ctx), True)
            ElseIf BeginsWith(ScriptLine, "unlock ") Then
                ExecuteLock(RetrieveParameter(ScriptLine, ctx), False)
            ElseIf BeginsWith(ScriptLine, "playmod ") And _gameAslVersion < 410 Then
                LogASLError("'playmod' is not supported in this version of Quest", LogType.WarningError)
            ElseIf BeginsWith(ScriptLine, "modvolume") And _gameAslVersion < 410 Then
                LogASLError("'modvolume' is not supported in this version of Quest", LogType.WarningError)
            ElseIf Trim(LCase(ScriptLine)) = "dontprocess" Then
                ctx.DontProcessCommand = True
            ElseIf BeginsWith(ScriptLine, "return ") Then
                ctx.FunctionReturnValue = RetrieveParameter(ScriptLine, ctx)
            Else
                If BeginsWith(ScriptLine, "'") = False Then
                    LogASLError("Unrecognized keyword. Line reads: '" & Trim(ReportErrorLine(ScriptLine)) & "'", LogType.WarningError)
                End If
            End If
        Catch
            Print("[An internal error occurred]", ctx)
            LogASLError(Err.Number & " - '" & Err.Description & "' occurred processing script line '" & ScriptLine & "'", LogType.InternalError)
        End Try
    End Sub

    Private Sub ExecuteEnter(ScriptLine As String, ctx As Context)
        _commandOverrideModeOn = True
        _commandOverrideVariable = RetrieveParameter(ScriptLine, ctx)

        ' Now, wait for CommandOverrideModeOn to be set
        ' to False by ExecCommand. Execution can then resume.

        ChangeState(State.Waiting, True)

        SyncLock _commandLock
            System.Threading.Monitor.Wait(_commandLock)
        End SyncLock

        _commandOverrideModeOn = False

        ' State will have been changed to Working when the user typed their response,
        ' and will be set back to Ready when the call to ExecCommand has finished
    End Sub

    Private Sub ExecuteSet(SetInstruction As String, ctx As Context)
        Dim i As Integer
        Dim TimerInterval As String
        Dim SCPos As Integer
        Dim TimerName As String
        Dim FoundTimer As Boolean

        If _gameAslVersion >= 280 Then
            If BeginsWith(SetInstruction, "interval ") Then
                TimerInterval = RetrieveParameter(SetInstruction, ctx)
                SCPos = InStr(TimerInterval, ";")
                If SCPos = 0 Then
                    LogASLError("Too few parameters in 'set " & SetInstruction & "'", LogType.WarningError)
                    Exit Sub
                End If

                TimerName = Trim(Left(TimerInterval, SCPos - 1))
                TimerInterval = CStr(Val(Trim(Mid(TimerInterval, SCPos + 1))))

                For i = 1 To _numberTimers
                    If LCase(TimerName) = LCase(_timers(i).TimerName) Then
                        FoundTimer = True
                        _timers(i).TimerInterval = CInt(TimerInterval)
                        i = _numberTimers
                    End If
                Next i

                If Not FoundTimer Then
                    LogASLError("No such timer '" & TimerName & "'", LogType.WarningError)
                    Exit Sub
                End If
            ElseIf BeginsWith(SetInstruction, "string ") Then
                ExecSetString(RetrieveParameter(SetInstruction, ctx), ctx)
            ElseIf BeginsWith(SetInstruction, "numeric ") Then
                ExecSetVar(RetrieveParameter(SetInstruction, ctx), ctx)
            ElseIf BeginsWith(SetInstruction, "collectable ") Then
                ExecuteSetCollectable(RetrieveParameter(SetInstruction, ctx), ctx)
            Else
                Dim Result = SetUnknownVariableType(RetrieveParameter(SetInstruction, ctx), ctx)
                If Result = SetResult.Error Then
                    LogASLError("Error on setting 'set " & SetInstruction & "'", LogType.WarningError)
                ElseIf Result = SetResult.Unfound Then
                    LogASLError("Variable type not specified in 'set " & SetInstruction & "'", LogType.WarningError)
                End If
            End If
        Else
            ExecuteSetCollectable(RetrieveParameter(SetInstruction, ctx), ctx)
        End If

    End Sub

    Private Function FindStatement(searchblock As DefineBlock, statement As String) As String
        Dim i As Integer

        ' Finds a statement within a given block of lines

        For i = searchblock.StartLine + 1 To searchblock.EndLine - 1

            ' Ignore sub-define blocks
            If BeginsWith(_lines(i), "define ") Then
                Do
                    i = i + 1
                Loop Until Trim(_lines(i)) = "end define"
            End If
            ' Check to see if the line matches the statement
            ' that is begin searched for
            If BeginsWith(_lines(i), statement) Then
                ' Return the parameters between < and > :
                Return RetrieveParameter(_lines(i), _nullContext)
            End If
        Next i

        Return ""
    End Function

    Private Function FindLine(searchblock As DefineBlock, statement As String, statementparam As String) As String
        Dim i As Integer
        ' Finds a statement within a given block of lines

        For i = searchblock.StartLine + 1 To searchblock.EndLine - 1

            ' Ignore sub-define blocks
            If BeginsWith(_lines(i), "define ") Then
                Do
                    i = i + 1
                Loop Until Trim(_lines(i)) = "end define"
            End If
            ' Check to see if the line matches the statement
            ' that is begin searched for
            If BeginsWith(_lines(i), statement) Then
                If UCase(Trim(RetrieveParameter(_lines(i), _nullContext))) = UCase(Trim(statementparam)) Then
                    Return Trim(_lines(i))
                End If
            End If
        Next i

        Return ""
    End Function

    Private Function GetCollectableAmount(colname As String) As Double
        Dim i As Integer

        For i = 1 To _numCollectables
            If _collectables(i).Name = colname Then
                Return _collectables(i).Value
            End If
        Next i

        Return 0
    End Function

    Private Function GetSecondChunk(l As String) As String
        Dim EndOfFirstBit, LengthOfKeyword As Integer
        Dim SecondChunk As String

        EndOfFirstBit = InStr(l, ">") + 1
        LengthOfKeyword = (Len(l) - EndOfFirstBit) + 1
        SecondChunk = Trim(Mid(l, EndOfFirstBit, LengthOfKeyword))

        Return SecondChunk
    End Function

    Private Sub GoDirection(Direction As String, ctx As Context)
        ' leaves the current room in direction specified by
        ' 'direction'

        Dim NewRoom As String
        Dim SCP As Integer

        Dim RoomID As Integer
        Dim DirData As New TextAction
        RoomID = GetRoomID(_currentRoom, ctx)

        If RoomID = 0 Then Exit Sub

        If _gameAslVersion >= 410 Then
            _rooms(RoomID).Exits.ExecuteGo(Direction, ctx)
            Exit Sub
        End If

        Dim r = _rooms(RoomID)

        If Direction = "north" Then
            DirData = r.North
        ElseIf Direction = "south" Then
            DirData = r.South
        ElseIf Direction = "west" Then
            DirData = r.West
        ElseIf Direction = "east" Then
            DirData = r.East
        ElseIf Direction = "northeast" Then
            DirData = r.NorthEast
        ElseIf Direction = "northwest" Then
            DirData = r.NorthWest
        ElseIf Direction = "southeast" Then
            DirData = r.SouthEast
        ElseIf Direction = "southwest" Then
            DirData = r.SouthWest
        ElseIf Direction = "up" Then
            DirData = r.Up
        ElseIf Direction = "down" Then
            DirData = r.Down
        ElseIf Direction = "out" Then
            If r.Out.Script = "" Then
                DirData.Data = r.Out.Text
                DirData.Type = TextActionType.Text
            Else
                DirData.Data = r.Out.Script
                DirData.Type = TextActionType.Script
            End If
        End If

        If DirData.Type = TextActionType.Script And DirData.Data <> "" Then
            ExecuteScript(DirData.Data, ctx)
        ElseIf DirData.Data <> "" Then
            NewRoom = DirData.Data
            SCP = InStr(NewRoom, ";")
            If SCP <> 0 Then
                NewRoom = Trim(Mid(NewRoom, SCP + 1))
            End If
            PlayGame(NewRoom, ctx)
        Else
            If Direction = "out" Then
                PlayerErrorMessage(PlayerError.DefaultOut, ctx)
            Else
                PlayerErrorMessage(PlayerError.BadPlace, ctx)
            End If
        End If

    End Sub

    Private Sub GoToPlace(placecheck As String, ctx As Context)
        ' leaves the current room in direction specified by
        ' 'direction'

        Dim DisallowedDirection As Boolean
        Dim NP As String, Destination As String = "", P, s As String
        DisallowedDirection = False

        P = PlaceExist(placecheck, ctx)

        If P <> "" Then
            Destination = P
        ElseIf BeginsWith(placecheck, "the ") Then
            NP = GetEverythingAfter(placecheck, "the ")
            P = PlaceExist(NP, ctx)
            If P <> "" Then
                Destination = P
            Else
                DisallowedDirection = True
            End If
        Else
            DisallowedDirection = True
        End If

        If Destination <> "" Then
            If InStr(Destination, ";") > 0 Then
                s = Trim(Right(Destination, Len(Destination) - InStr(Destination, ";")))
                ExecuteScript(s, ctx)
            Else
                PlayGame(Destination, ctx)
            End If
        End If

        If DisallowedDirection = True Then
            PlayerErrorMessage(PlayerError.BadPlace, ctx)
        End If

    End Sub

    Private Function InitialiseGame(afilename As String, Optional LoadedFromQSG As Boolean = False) As Boolean
        Dim GameType, ErrorString As String
        Dim i As Integer
        Dim ASLVersion As String

        _loadedFromQsg = LoadedFromQSG

        _changeLogRooms = New ChangeLog
        _changeLogObjects = New ChangeLog
        _changeLogRooms.AppliesToType = ChangeLog.eAppliesToType.atRoom
        _changeLogObjects.AppliesToType = ChangeLog.eAppliesToType.atObject

        _outPutOn = True
        _useAbbreviations = True

        _gamePath = System.IO.Path.GetDirectoryName(afilename) + "\"

        Dim LogMsg As String
        LogMsg = "Opening file " & afilename & " on " & Date.Now.ToString()
        LogASLError(LogMsg, LogType.Init)

        ' Parse file and find where the 'define' blocks are:
        If ParseFile(afilename) = False Then
            LogASLError("Unable to open file", LogType.Init)
            ErrorString = "Unable to open " & afilename

            If _openErrorReport <> "" Then
                ' Strip last vbcrlf
                _openErrorReport = Left(_openErrorReport, Len(_openErrorReport) - 2)
                ErrorString = ErrorString & ":" & vbCrLf & vbCrLf & _openErrorReport
            End If

            Print("Error: " & ErrorString, _nullContext)

            Return False
        End If

        ' Check version
        Dim gameblock As DefineBlock
        gameblock = GetDefineBlock("game")

        ASLVersion = "//"
        GameType = ""
        For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
            If BeginsWith(_lines(i), "asl-version ") Then
                ASLVersion = RetrieveParameter(_lines(i), _nullContext)
            ElseIf BeginsWith(_lines(i), "gametype ") Then
                GameType = GetEverythingAfter(_lines(i), "gametype ")
            End If
        Next i

        If ASLVersion = "//" Then
            LogASLError("File contains no version header.", LogType.WarningError)
        Else
            _gameAslVersion = CInt(ASLVersion)

            Const RecognisedVersions As String = "/100/200/210/217/280/281/282/283/284/285/300/310/311/320/350/390/391/392/400/410/"

            If InStr(RecognisedVersions, "/" & ASLVersion & "/") = 0 Then
                LogASLError("Unrecognised ASL version number.", LogType.WarningError)
            End If
        End If

        m_listVerbs.Add(ListType.ExitsList, New List(Of String)(New String() {"Go to"}))

        If _gameAslVersion >= 280 And _gameAslVersion < 390 Then
            m_listVerbs.Add(ListType.ObjectsList, New List(Of String)(New String() {"Look at", "Examine", "Take", "Speak to"}))
            m_listVerbs.Add(ListType.InventoryList, New List(Of String)(New String() {"Look at", "Examine", "Use", "Drop"}))
        Else
            m_listVerbs.Add(ListType.ObjectsList, New List(Of String)(New String() {"Look at", "Take", "Speak to"}))
            m_listVerbs.Add(ListType.InventoryList, New List(Of String)(New String() {"Look at", "Use", "Drop"}))
        End If

        ' Get the name of the game:
        _gameName = RetrieveParameter(_lines(GetDefineBlock("game").StartLine), _nullContext)

        m_player.UpdateGameName(_gameName)
        m_player.Show("Panes")
        m_player.Show("Location")
        m_player.Show("Command")

        SetUpGameObject()
        SetUpOptions()

        For i = GetDefineBlock("game").StartLine + 1 To GetDefineBlock("game").EndLine - 1
            If BeginsWith(_lines(i), "beforesave ") Then
                _beforeSaveScript = GetEverythingAfter(_lines(i), "beforesave ")
            ElseIf BeginsWith(_lines(i), "onload ") Then
                _onLoadScript = GetEverythingAfter(_lines(i), "onload ")

            End If
        Next i

        SetDefaultPlayerErrorMessages()

        SetUpSynonyms()
        SetUpRoomData()

        If _gameAslVersion >= 410 Then
            SetUpExits()
        End If

        If _gameAslVersion < 280 Then
            ' Set up an array containing the names of all the items
            ' used in the game, based on the possitems statement
            ' of the 'define game' block.
            SetUpItemArrays()
        End If

        If _gameAslVersion < 280 Then
            ' Now, go through the 'startitems' statement and set up
            ' the items array so we start with those items mentioned.
            SetUpStartItems()
        End If

        ' Set up collectables.
        SetUpCollectables()

        SetUpDisplayVariables()

        ' Set up characters and objects.
        SetUpCharObjectInfo()
        SetUpUserDefinedPlayerErrors()
        SetUpDefaultFonts()
        SetUpTurnScript()
        SetUpTimers()
        SetUpMenus()

        _gameFileName = afilename

        LogASLError("Finished loading file.", LogType.Init)

        _defaultRoomProperties = GetPropertiesInType("defaultroom", False)
        _defaultProperties = GetPropertiesInType("default", False)

        Return True
    End Function

    Private Sub LeaveRoom(ctx As Context)
        ' leaves the current room
        GoDirection("out", ctx)
    End Sub

    Private Function PlaceExist(PlaceName As String, ctx As Context) As String
        ' Returns actual name of an available "place" exit, and if
        ' script is executed on going in that direction, that script
        ' is returned after a ";"

        Dim RoomID As Integer
        Dim CheckPlace As String
        Dim DestRoomID As Integer
        Dim CheckPlaceName As String
        Dim FoundPlace, ScriptPresent As Boolean
        Dim i As Integer

        RoomID = GetRoomID(_currentRoom, ctx)
        FoundPlace = False
        ScriptPresent = False

        ' check if place is available
        Dim r = _rooms(RoomID)

        For i = 1 To r.NumberPlaces
            CheckPlace = r.Places(i).PlaceName

            'remove any prefix and semicolon
            If InStr(CheckPlace, ";") > 0 Then
                CheckPlace = Trim(Right(CheckPlace, Len(CheckPlace) - (InStr(CheckPlace, ";") + 1)))
            End If

            CheckPlaceName = CheckPlace

            If _gameAslVersion >= 311 And r.Places(i).Script = "" Then
                DestRoomID = GetRoomID(CheckPlace, ctx)
                If DestRoomID <> 0 Then
                    If _rooms(DestRoomID).RoomAlias <> "" Then
                        CheckPlaceName = _rooms(DestRoomID).RoomAlias
                    End If
                End If
            End If

            If LCase(CheckPlaceName) = LCase(PlaceName) Then
                FoundPlace = True

                If r.Places(i).Script <> "" Then
                    Return CheckPlace & ";" & r.Places(i).Script
                Else
                    Return CheckPlace
                End If
            End If
        Next i

        Return ""
    End Function

    Private Sub PlayerItem(anitem As String, gotit As Boolean, ctx As Context, Optional ObjID As Integer = 0)
        ' Gives the player an item (if gotit=True) or takes an
        ' item away from the player (if gotit=False).

        ' If ASL>280, setting gotit=TRUE moves specified
        ' *object* to room "inventory"; setting gotit=FALSe
        ' drops object into current room.

        Dim FoundObjectName As Boolean
        FoundObjectName = False
        Dim OldRoom As String
        Dim i As Integer

        If _gameAslVersion >= 280 Then
            If ObjID = 0 Then
                For i = 1 To _numberObjs
                    If LCase(_objs(i).ObjectName) = LCase(anitem) Then
                        ObjID = i
                        i = _numberObjs
                    End If
                Next i
            End If

            If ObjID <> 0 Then
                OldRoom = LCase(_objs(ObjID).ContainerRoom)
                If gotit Then
                    If _gameAslVersion >= 391 Then
                        ' Unset parent information, if any
                        AddToObjectProperties("not parent", ObjID, ctx)
                    End If
                    MoveThing(_objs(ObjID).ObjectName, "inventory", Thing.Object, ctx)

                    If _objs(ObjID).GainScript <> "" Then
                        ExecuteScript(_objs(ObjID).GainScript, ctx)
                    End If
                Else
                    MoveThing(_objs(ObjID).ObjectName, _currentRoom, Thing.Object, ctx)

                    If _objs(ObjID).LoseScript <> "" Then
                        ExecuteScript(_objs(ObjID).LoseScript, ctx)
                    End If

                End If

                FoundObjectName = True
            End If

            If Not FoundObjectName Then
                LogASLError("No such object '" & anitem & "'", LogType.WarningError)
            Else
                UpdateItems(ctx)
                UpdateObjectList(ctx)
            End If
        Else
            For i = 1 To _numberItems
                If _items(i).Name = anitem Then
                    _items(i).Got = gotit
                    i = _numberItems
                End If
            Next i

            UpdateItems(ctx)

        End If
    End Sub

    Friend Sub PlayGame(Room As String, ctx As Context)
        'plays the specified room

        Dim RoomID As Integer
        Dim RoomScript As String
        RoomID = GetRoomID(Room, ctx)

        If RoomID = 0 Then
            LogASLError("No such room '" & Room & "'", LogType.WarningError)
            Exit Sub
        End If

        Dim LastRoom As String
        LastRoom = _currentRoom

        _currentRoom = Room

        SetStringContents("quest.currentroom", Room, ctx)

        If _gameAslVersion >= 391 And _gameAslVersion < 410 Then
            AddToObjectProperties("visited", _rooms(RoomID).ObjID, ctx)
        End If

        ShowRoomInfo((Room), ctx)

        UpdateItems(ctx)

        ' Find script lines and execute them.

        If _rooms(RoomID).Script <> "" Then
            RoomScript = _rooms(RoomID).Script
            ExecuteScript(RoomScript, ctx)
        End If

        If _gameAslVersion >= 410 Then
            AddToObjectProperties("visited", _rooms(RoomID).ObjID, ctx)
        End If
    End Sub

    Friend Sub Print(txt As String, ctx As Context, Optional OutputTo As String = "normal", Optional NoTalk As Boolean = False)
        Dim i As Integer
        Dim PrintString As String
        Dim PrintThis As Boolean

        PrintString = ""

        If txt = "" Then
            DoPrint(PrintString)
        Else
            For i = 1 To Len(txt)

                PrintThis = True

                If Mid(txt, i, 2) = "|w" Then
                    'CodesList = CodesList & "w"
                    'InputString = Left$(InputString, i - 1) & Mid$(InputString, i + 2)
                    DoPrint(PrintString)
                    PrintString = ""
                    PrintThis = False
                    i = i + 1

                    ExecuteScript("wait <>", ctx)

                ElseIf Mid(txt, i, 2) = "|c" Then
                    Select Case Mid(txt, i, 3)
                        Case "|cb", "|cr", "|cl", "|cy", "|cg"
                            ' Do nothing - we don't want to remove the colour formatting codes.
                        Case Else
                            'CodesList = CodesList & "c"
                            'InputString = Left$(InputString, i - 1) & Mid$(InputString, i + 2)
                            DoPrint(PrintString)
                            PrintString = ""
                            PrintThis = False
                            i = i + 1

                            ExecuteScript("clear", ctx)
                    End Select
                End If

                If PrintThis Then PrintString = PrintString & Mid(txt, i, 1)

            Next i

            If PrintString <> "" Then DoPrint(PrintString)
        End If
    End Sub

    Private Function RetrLine(BlockType As String, blockparam As String, lineret As String, ctx As Context) As String
        'retrieves the line lineret in the block of type blocktype
        'with parameter blockparam in the current room/game block

        Dim i As Integer
        Dim searchblock As DefineBlock

        If BlockType = "object" Then
            searchblock = GetThingBlock(blockparam, _currentRoom, Thing.Object)
        Else
            searchblock = GetThingBlock(blockparam, _currentRoom, Thing.Character)
        End If

        If searchblock.StartLine = 0 And searchblock.EndLine = 0 Then
            Return "<undefined>"
        End If

        For i = searchblock.StartLine + 1 To searchblock.EndLine - 1
            If BeginsWith(_lines(i), lineret) Then
                Return Trim(_lines(i))
            End If
        Next i

        Return "<unfound>"
    End Function

    Private Function RetrLineParam(BlockType As String, blockparam As String, lineret As String, lineparam As String, ctx As Context) As String
        'retrieves the line lineret with parameter lineparam
        'in the block of type blocktype with parameter blockparam
        'in the current room - of course.

        Dim i As Integer
        Dim searchblock As DefineBlock

        If BlockType = "object" Then
            searchblock = GetThingBlock(blockparam, _currentRoom, Thing.Object)
        Else
            searchblock = GetThingBlock(blockparam, _currentRoom, Thing.Character)
        End If

        If searchblock.StartLine = 0 And searchblock.EndLine = 0 Then
            Return "<undefined>"
        End If

        For i = searchblock.StartLine + 1 To searchblock.EndLine - 1
            If BeginsWith(_lines(i), lineret) AndAlso LCase(RetrieveParameter(_lines(i), ctx)) = LCase(lineparam) Then
                Return Trim(_lines(i))
            End If
        Next i

        Return "<unfound>"
    End Function

    Private Sub SetUpCollectables()
        Dim LastItem As Boolean
        Dim CharPos, a, NextComma As Integer
        Dim PossItems As String
        Dim CInfo, T As String
        Dim SpacePos, EqualsPos, Space2Pos As Integer
        Dim i, b As String
        Dim Bpos1, BPos2 As Integer
        LastItem = False

        _numCollectables = 0

        ' Initialise collectables:
        ' First, find the collectables section within the define
        ' game block, and get its parameters:

        For a = GetDefineBlock("game").StartLine + 1 To GetDefineBlock("game").EndLine - 1
            If BeginsWith(_lines(a), "collectables ") Then
                PossItems = Trim(RetrieveParameter(_lines(a), _nullContext, False))

                ' if collectables is a null string, there are no
                ' collectables. Otherwise, there is one more object than
                ' the number of commas. So, first check to see if we have
                ' no objects:

                If PossItems <> "" Then
                    _numCollectables = 1
                    CharPos = 1
                    Do
                        ReDim Preserve _collectables(_numCollectables)
                        _collectables(_numCollectables) = New Collectable
                        NextComma = InStr(CharPos + 1, PossItems, ",")
                        If NextComma = 0 Then
                            NextComma = InStr(CharPos + 1, PossItems, ";")
                        End If

                        'If there are no more commas, we want everything
                        'up to the end of the string, and then to exit
                        'the loop:
                        If NextComma = 0 Then
                            NextComma = Len(PossItems) + 1
                            LastItem = True
                        End If

                        'Get item info
                        CInfo = Trim(Mid(PossItems, CharPos, NextComma - CharPos))
                        _collectables(_numCollectables).Name = Trim(Left(CInfo, InStr(CInfo, " ")))

                        EqualsPos = InStr(CInfo, "=")
                        SpacePos = InStr(CInfo, " ")
                        Space2Pos = InStr(EqualsPos, CInfo, " ")
                        If Space2Pos = 0 Then Space2Pos = Len(CInfo) + 1
                        T = Trim(Mid(CInfo, SpacePos + 1, EqualsPos - SpacePos - 1))
                        i = Trim(Mid(CInfo, EqualsPos + 1, Space2Pos - EqualsPos - 1))

                        If Left(T, 1) = "d" Then
                            T = Mid(T, 2)
                            _collectables(_numCollectables).DisplayWhenZero = False
                        Else
                            _collectables(_numCollectables).DisplayWhenZero = True
                        End If

                        _collectables(_numCollectables).Type = T
                        _collectables(_numCollectables).Value = Val(i)

                        ' Get display string between square brackets
                        Bpos1 = InStr(CInfo, "[")
                        BPos2 = InStr(CInfo, "]")
                        If Bpos1 = 0 Then
                            _collectables(_numCollectables).Display = "<def>"
                        Else
                            b = Mid(CInfo, Bpos1 + 1, (BPos2 - 1) - Bpos1)
                            _collectables(_numCollectables).Display = Trim(b)
                        End If

                        CharPos = NextComma + 1
                        _numCollectables = _numCollectables + 1

                        'lastitem set when nextcomma=0, above.
                    Loop Until LastItem = True
                    _numCollectables = _numCollectables - 1
                End If
            End If
        Next a
    End Sub

    Private Sub SetUpItemArrays()
        Dim LastItem As Boolean
        Dim CharPos, a, NextComma As Integer
        Dim PossItems As String
        LastItem = False

        _numberItems = 0

        ' Initialise items:
        ' First, find the possitems section within the define game
        ' block, and get its parameters:
        For a = GetDefineBlock("game").StartLine + 1 To GetDefineBlock("game").EndLine - 1
            If BeginsWith(_lines(a), "possitems ") Or BeginsWith(_lines(a), "items ") Then
                PossItems = RetrieveParameter(_lines(a), _nullContext)

                If PossItems <> "" Then
                    _numberItems = _numberItems + 1
                    CharPos = 1
                    Do
                        ReDim Preserve _items(_numberItems)
                        _items(_numberItems) = New ItemType
                        NextComma = InStr(CharPos + 1, PossItems, ",")
                        If NextComma = 0 Then
                            NextComma = InStr(CharPos + 1, PossItems, ";")
                        End If

                        'If there are no more commas, we want everything
                        'up to the end of the string, and then to exit
                        'the loop:
                        If NextComma = 0 Then
                            NextComma = Len(PossItems) + 1
                            LastItem = True
                        End If

                        'Get item name
                        _items(_numberItems).Name = Trim(Mid(PossItems, CharPos, NextComma - CharPos))
                        _items(_numberItems).Got = False

                        CharPos = NextComma + 1
                        _numberItems = _numberItems + 1

                        'lastitem set when nextcomma=0, above.
                    Loop Until LastItem = True
                    _numberItems = _numberItems - 1
                End If
            End If
        Next a
    End Sub

    Private Sub SetUpStartItems()
        Dim CharPos, a, NextComma As Integer
        Dim StartItems As String
        Dim LastItem As Boolean
        Dim TheItemName As String
        Dim i As Integer

        For a = GetDefineBlock("game").StartLine + 1 To GetDefineBlock("game").EndLine - 1
            If BeginsWith(_lines(a), "startitems ") Then
                StartItems = RetrieveParameter(_lines(a), _nullContext)

                If StartItems <> "" Then
                    CharPos = 1
                    Do
                        NextComma = InStr(CharPos + 1, StartItems, ",")
                        If NextComma = 0 Then
                            NextComma = InStr(CharPos + 1, StartItems, ";")
                        End If

                        'If there are no more commas, we want everything
                        'up to the end of the string, and then to exit
                        'the loop:
                        If NextComma = 0 Then
                            NextComma = Len(StartItems) + 1
                            LastItem = True
                        End If

                        'Get item name
                        TheItemName = Trim(Mid(StartItems, CharPos, NextComma - CharPos))

                        'Find which item this is, and set it
                        For i = 1 To _numberItems
                            If _items(i).Name = TheItemName Then
                                _items(i).Got = True
                                i = _numberItems
                            End If
                        Next i

                        CharPos = NextComma + 1

                        'lastitem set when nextcomma=0, above.
                    Loop Until LastItem = True
                End If
            End If
        Next a
    End Sub

    Private Sub ShowHelp(ctx As Context)
        ' In Quest 4 and below, the help text displays in a separate window. In Quest 5, it displays
        ' in the same window as the game text.
        Print("|b|cl|s14Quest Quick Help|xb|cb|s00", ctx, "help")
        Print("", ctx, "help")
        Print("|cl|bMoving|xb|cb Press the direction buttons in the 'Compass' pane, or type |bGO NORTH|xb, |bSOUTH|xb, |bE|xb, etc. |xn", ctx, "help")
        Print("To go into a place, type |bGO TO ...|xb . To leave a place, type |bOUT, EXIT|xb or |bLEAVE|xb, or press the '|crOUT|cb' button.|n", ctx, "help")
        Print("|cl|bObjects and Characters|xb|cb Use |bTAKE ...|xb, |bGIVE ... TO ...|xb, |bTALK|xb/|bSPEAK TO ...|xb, |bUSE ... ON|xb/|bWITH ...|xb, |bLOOK AT ...|xb, etc.|n", ctx, "help")
        Print("|cl|bExit Quest|xb|cb Type |bQUIT|xb to leave Quest.|n", ctx, "help")
        Print("|cl|bMisc|xb|cb Type |bABOUT|xb to get information on the current game. The next turn after referring to an object or character, you can use |bIT|xb, |bHIM|xb etc. as appropriate to refer to it/him/etc. again. If you make a mistake when typing an object's name, type |bOOPS|xb followed by your correction.|n", ctx, "help")
        Print("|cl|bKeyboard shortcuts|xb|cb Press the |crup arrow|cb and |crdown arrow|cb to scroll through commands you have already typed in. Press |crEsc|cb to clear the command box.|n|n", ctx, "help")
        Print("Further information is available by selecting |iQuest Documentation|xi from the |iHelp|xi menu.", ctx, "help")
    End Sub

    Private Sub ReadCatalog(CatData As String)
        Dim i, Chr0Pos, ResourceStart As Integer

        Chr0Pos = InStr(CatData, Chr(0))
        _numResources = CInt(DecryptString(Left(CatData, Chr0Pos - 1)))
        ReDim Preserve _resources(_numResources)
        _resources(_numResources) = New ResourceType

        CatData = Mid(CatData, Chr0Pos + 1)

        ResourceStart = 0

        For i = 1 To _numResources
            Dim r = _resources(i)
            Chr0Pos = InStr(CatData, Chr(0))
            r.ResourceName = DecryptString(Left(CatData, Chr0Pos - 1))
            CatData = Mid(CatData, Chr0Pos + 1)

            Chr0Pos = InStr(CatData, Chr(0))
            r.ResourceLength = CInt(DecryptString(Left(CatData, Chr0Pos - 1)))
            CatData = Mid(CatData, Chr0Pos + 1)

            r.ResourceStart = ResourceStart
            ResourceStart = ResourceStart + r.ResourceLength

            r.Extracted = False
        Next i
    End Sub

    Private Sub UpdateDirButtons(AvailableDirs As String, ctx As Context)
        Dim compassExits As New List(Of ListData)

        If InStr(AvailableDirs, "n") > 0 Then
            AddCompassExit(compassExits, "north")
        End If

        If InStr(AvailableDirs, "s") > 0 Then
            AddCompassExit(compassExits, "south")
        End If

        If InStr(AvailableDirs, "w") > 0 Then
            AddCompassExit(compassExits, "west")
        End If

        If InStr(AvailableDirs, "e") > 0 Then
            AddCompassExit(compassExits, "east")
        End If

        If InStr(AvailableDirs, "o") > 0 Then
            AddCompassExit(compassExits, "out")
        End If

        If InStr(AvailableDirs, "a") > 0 Then
            AddCompassExit(compassExits, "northeast")
        End If

        If InStr(AvailableDirs, "b") > 0 Then
            AddCompassExit(compassExits, "northwest")
        End If

        If InStr(AvailableDirs, "c") > 0 Then
            AddCompassExit(compassExits, "southeast")
        End If

        If InStr(AvailableDirs, "d") > 0 Then
            AddCompassExit(compassExits, "southwest")
        End If

        If InStr(AvailableDirs, "u") > 0 Then
            AddCompassExit(compassExits, "up")
        End If

        If InStr(AvailableDirs, "f") > 0 Then
            AddCompassExit(compassExits, "down")
        End If

        _compassExits = compassExits
        UpdateExitsList()
    End Sub

    Private Sub AddCompassExit(exitList As List(Of ListData), name As String)
        exitList.Add(New ListData(name, m_listVerbs(ListType.ExitsList)))
    End Sub

    Private Function UpdateDoorways(RoomID As Integer, ctx As Context) As String
        Dim RoomDisplayText As String = "", OutPlace As String = ""
        Dim SCP As Integer
        Dim Directions As String = "", NSEW As String = "", OutPlaceName As String = ""
        Dim OutPlaceAlias As String
        Dim CommaPos As Integer
        Dim bFinNSEW As Boolean
        Dim NewCommaPos As Integer
        Dim OutPlacePrefix As String = ""

        Dim e, n, s, W As String
        Dim SE, NE, NW, SW As String
        Dim D, U, O As String

        n = "north"
        s = "south"
        e = "east"
        W = "west"
        NE = "northeast"
        NW = "northwest"
        SE = "southeast"
        SW = "southwest"
        U = "up"
        D = "down"
        O = "out"

        If _gameAslVersion >= 410 Then
            _rooms(RoomID).Exits.GetAvailableDirectionsDescription(RoomDisplayText, Directions)
        Else

            If _rooms(RoomID).Out.Text <> "" Then
                OutPlace = _rooms(RoomID).Out.Text

                'remove any prefix semicolon from printed text
                SCP = InStr(OutPlace, ";")
                If SCP = 0 Then
                    OutPlaceName = OutPlace
                Else
                    OutPlaceName = Trim(Mid(OutPlace, SCP + 1))
                    OutPlacePrefix = Trim(Left(OutPlace, SCP - 1))
                    OutPlace = OutPlacePrefix & " " & OutPlaceName
                End If
            End If

            If _rooms(RoomID).North.Data <> "" Then
                NSEW = NSEW & "|b" & n & "|xb, "
                Directions = Directions & "n"
            End If
            If _rooms(RoomID).South.Data <> "" Then
                NSEW = NSEW & "|b" & s & "|xb, "
                Directions = Directions & "s"
            End If
            If _rooms(RoomID).East.Data <> "" Then
                NSEW = NSEW & "|b" & e & "|xb, "
                Directions = Directions & "e"
            End If
            If _rooms(RoomID).West.Data <> "" Then
                NSEW = NSEW & "|b" & W & "|xb, "
                Directions = Directions & "w"
            End If
            If _rooms(RoomID).NorthEast.Data <> "" Then
                NSEW = NSEW & "|b" & NE & "|xb, "
                Directions = Directions & "a"
            End If
            If _rooms(RoomID).NorthWest.Data <> "" Then
                NSEW = NSEW & "|b" & NW & "|xb, "
                Directions = Directions & "b"
            End If
            If _rooms(RoomID).SouthEast.Data <> "" Then
                NSEW = NSEW & "|b" & SE & "|xb, "
                Directions = Directions & "c"
            End If
            If _rooms(RoomID).SouthWest.Data <> "" Then
                NSEW = NSEW & "|b" & SW & "|xb, "
                Directions = Directions & "d"
            End If
            If _rooms(RoomID).Up.Data <> "" Then
                NSEW = NSEW & "|b" & U & "|xb, "
                Directions = Directions & "u"
            End If
            If _rooms(RoomID).Down.Data <> "" Then
                NSEW = NSEW & "|b" & D & "|xb, "
                Directions = Directions & "f"
            End If

            If OutPlace <> "" Then
                'see if outside has an alias

                OutPlaceAlias = _rooms(GetRoomID(OutPlaceName, ctx)).RoomAlias
                If OutPlaceAlias = "" Then
                    OutPlaceAlias = OutPlace
                Else
                    If _gameAslVersion >= 360 Then
                        If OutPlacePrefix <> "" Then
                            OutPlaceAlias = OutPlacePrefix & " " & OutPlaceAlias
                        End If
                    End If
                End If

                RoomDisplayText = RoomDisplayText & "You can go |bout|xb to " & OutPlaceAlias & "."
                If NSEW <> "" Then RoomDisplayText = RoomDisplayText & " "

                Directions = Directions & "o"
                If _gameAslVersion >= 280 Then
                    SetStringContents("quest.doorways.out", OutPlaceName, ctx)
                Else
                    SetStringContents("quest.doorways.out", OutPlaceAlias, ctx)
                End If
                SetStringContents("quest.doorways.out.display", OutPlaceAlias, ctx)
            Else
                SetStringContents("quest.doorways.out", "", ctx)
                SetStringContents("quest.doorways.out.display", "", ctx)
            End If

            If NSEW <> "" Then
                'strip final comma
                NSEW = Left(NSEW, Len(NSEW) - 2)
                CommaPos = InStr(NSEW, ",")
                If CommaPos <> 0 Then
                    bFinNSEW = False
                    Do
                        NewCommaPos = InStr(CommaPos + 1, NSEW, ",")
                        If NewCommaPos = 0 Then
                            bFinNSEW = True
                        Else
                            CommaPos = NewCommaPos
                        End If
                    Loop Until bFinNSEW

                    NSEW = Trim(Left(NSEW, CommaPos - 1)) & " or " & Trim(Mid(NSEW, CommaPos + 1))
                End If

                RoomDisplayText = RoomDisplayText & "You can go " & NSEW & "."
                SetStringContents("quest.doorways.dirs", NSEW, ctx)
            Else
                SetStringContents("quest.doorways.dirs", "", ctx)
            End If
        End If

        UpdateDirButtons(Directions, ctx)

        Return RoomDisplayText
    End Function

    Private Sub UpdateItems(ctx As Context)
        ' displays the items a player has
        Dim i, j As Integer
        Dim k As String
        Dim invList As New List(Of ListData)

        If Not _outPutOn Then Exit Sub

        Dim CurObjName As String

        If _gameAslVersion >= 280 Then
            For i = 1 To _numberObjs
                If _objs(i).ContainerRoom = "inventory" And _objs(i).Exists And _objs(i).Visible Then
                    If _objs(i).ObjectAlias = "" Then
                        CurObjName = _objs(i).ObjectName
                    Else
                        CurObjName = _objs(i).ObjectAlias
                    End If

                    invList.Add(New ListData(CapFirst(CurObjName), m_listVerbs(ListType.InventoryList)))

                End If
            Next i
        Else
            For j = 1 To _numberItems
                If _items(j).Got = True Then
                    invList.Add(New ListData(CapFirst(_items(j).Name), m_listVerbs(ListType.InventoryList)))
                End If
            Next j
        End If

        RaiseEvent UpdateList(ListType.InventoryList, invList)

        If _gameAslVersion >= 284 Then
            UpdateStatusVars(ctx)
        Else
            If _numCollectables > 0 Then

                Dim status As String = ""

                For j = 1 To _numCollectables
                    k = DisplayCollectableInfo(j)
                    If k <> "<null>" Then
                        If status.Length > 0 Then status += Environment.NewLine
                        status += k
                    End If
                Next j

                m_player.SetStatusText(status)

            End If
        End If
    End Sub

    Private Sub FinishGame(wingame As StopType, ctx As Context)
        If wingame = StopType.Win Then
            DisplayTextSection("win", ctx)
        ElseIf wingame = StopType.Lose Then
            DisplayTextSection("lose", ctx)
        End If

        GameFinished()
    End Sub

    Private Sub UpdateObjectList(ctx As Context)
        ' Updates object list
        Dim i, PlaceID As Integer
        Dim ShownPlaceName As String
        Dim ObjSuffix As String, CharsViewable As String = ""
        Dim CharsFound As Integer
        Dim NoFormatObjsViewable, CharList As String, ObjsViewable As String = ""
        Dim ObjsFound As Integer
        Dim ObjListString, NFObjListString As String

        If Not _outPutOn Then Exit Sub

        Dim objList As New List(Of ListData)
        Dim exitList As New List(Of ListData)

        'find the room
        Dim roomblock As DefineBlock
        roomblock = DefineBlockParam("room", _currentRoom)

        'FIND CHARACTERS ===
        If _gameAslVersion < 281 Then
            ' go through Chars() array
            For i = 1 To _numberChars
                If _chars(i).ContainerRoom = _currentRoom And _chars(i).Exists And _chars(i).Visible Then
                    AddToObjectList(objList, exitList, _chars(i).ObjectName, Thing.Character)
                    CharsViewable = CharsViewable & _chars(i).Prefix & "|b" & _chars(i).ObjectName & "|xb" & _chars(i).Suffix & ", "
                    CharsFound = CharsFound + 1
                End If
            Next i

            If CharsFound = 0 Then
                SetStringContents("quest.characters", "", ctx)
            Else
                'chop off final comma and add full stop (.)
                CharList = Left(CharsViewable, Len(CharsViewable) - 2)
                SetStringContents("quest.characters", CharList, ctx)
            End If
        End If

        'FIND OBJECTS
        NoFormatObjsViewable = ""

        For i = 1 To _numberObjs
            If LCase(_objs(i).ContainerRoom) = LCase(_currentRoom) And _objs(i).Exists And _objs(i).Visible And Not _objs(i).IsExit Then
                ObjSuffix = _objs(i).Suffix
                If ObjSuffix <> "" Then ObjSuffix = " " & ObjSuffix
                If _objs(i).ObjectAlias = "" Then
                    AddToObjectList(objList, exitList, _objs(i).ObjectName, Thing.Object)
                    ObjsViewable = ObjsViewable & _objs(i).Prefix & "|b" & _objs(i).ObjectName & "|xb" & ObjSuffix & ", "
                    NoFormatObjsViewable = NoFormatObjsViewable & _objs(i).Prefix & _objs(i).ObjectName & ", "
                Else
                    AddToObjectList(objList, exitList, _objs(i).ObjectAlias, Thing.Object)
                    ObjsViewable = ObjsViewable & _objs(i).Prefix & "|b" & _objs(i).ObjectAlias & "|xb" & ObjSuffix & ", "
                    NoFormatObjsViewable = NoFormatObjsViewable & _objs(i).Prefix & _objs(i).ObjectAlias & ", "
                End If
                ObjsFound = ObjsFound + 1
            End If
        Next i

        If ObjsFound <> 0 Then
            ObjListString = Left(ObjsViewable, Len(ObjsViewable) - 2)
            NFObjListString = Left(NoFormatObjsViewable, Len(NoFormatObjsViewable) - 2)
            SetStringContents("quest.objects", Left(NoFormatObjsViewable, Len(NoFormatObjsViewable) - 2), ctx)
            SetStringContents("quest.formatobjects", ObjListString, ctx)
        Else
            SetStringContents("quest.objects", "", ctx)
            SetStringContents("quest.formatobjects", "", ctx)
        End If

        'FIND DOORWAYS
        Dim RoomID As Integer
        RoomID = GetRoomID(_currentRoom, ctx)

        Dim r = _rooms(RoomID)

        If _gameAslVersion >= 410 Then

            If RoomID > 0 Then
                For Each oExit As RoomExit In _rooms(RoomID).Exits.Places.Values
                    AddToObjectList(objList, exitList, oExit.DisplayName, Thing.Room)
                Next
            End If

        Else
            For i = 1 To r.NumberPlaces

                If _gameAslVersion >= 311 And _rooms(RoomID).Places(i).Script = "" Then
                    PlaceID = GetRoomID(_rooms(RoomID).Places(i).PlaceName, ctx)
                    If PlaceID = 0 Then
                        ShownPlaceName = _rooms(RoomID).Places(i).PlaceName
                    Else
                        If _rooms(PlaceID).RoomAlias <> "" Then
                            ShownPlaceName = _rooms(PlaceID).RoomAlias
                        Else
                            ShownPlaceName = _rooms(RoomID).Places(i).PlaceName
                        End If
                    End If
                Else
                    ShownPlaceName = _rooms(RoomID).Places(i).PlaceName
                End If

                AddToObjectList(objList, exitList, ShownPlaceName, Thing.Room)
            Next i
        End If

        RaiseEvent UpdateList(ListType.ObjectsList, objList)
        _gotoExits = exitList
        UpdateExitsList()
    End Sub

    Private Sub UpdateExitsList()
        ' The Quest 5.0 Player takes a combined list of compass and "go to" exits, whereas the
        ' ASL4 code produces these separately. So we keep track of them separately and then
        ' merge to send to the Player.

        Dim mergedList As New List(Of ListData)

        For Each listItem As ListData In _compassExits
            mergedList.Add(listItem)
        Next

        For Each listItem As ListData In _gotoExits
            mergedList.Add(listItem)
        Next

        RaiseEvent UpdateList(ListType.ExitsList, mergedList)
    End Sub

    Private Sub UpdateStatusVars(ctx As Context)
        Dim DisplayData As String
        Dim i As Integer
        Dim status As String = ""

        If _numDisplayStrings > 0 Then
            For i = 1 To _numDisplayStrings
                DisplayData = DisplayStatusVariableInfo(i, VarType.String, ctx)

                If DisplayData <> "" Then
                    If status.Length > 0 Then status += Environment.NewLine
                    status += DisplayData
                End If
            Next i
        End If

        If _numDisplayNumerics > 0 Then
            For i = 1 To _numDisplayNumerics
                DisplayData = DisplayStatusVariableInfo(i, VarType.Numeric, ctx)
                If DisplayData <> "" Then
                    If status.Length > 0 Then status += Environment.NewLine
                    status += DisplayData
                End If
            Next i
        End If

        m_player.SetStatusText(status)
    End Sub

    Private Sub UpdateVisibilityInContainers(ctx As Context, Optional OnlyParent As String = "")
        ' Use OnlyParent to only update objects that are contained by a specific parent

        Dim i, ParentID As Integer
        Dim Parent As String
        Dim ParentIsTransparent, ParentIsOpen, ParentIsSeen As Boolean
        Dim ParentIsSurface As Boolean

        If _gameAslVersion < 391 Then Exit Sub

        If OnlyParent <> "" Then
            OnlyParent = LCase(OnlyParent)
            ParentID = GetObjectIDNoAlias(OnlyParent)

            ParentIsOpen = IsYes(GetObjectProperty("opened", ParentID, True, False))
            ParentIsTransparent = IsYes(GetObjectProperty("transparent", ParentID, True, False))
            ParentIsSeen = IsYes(GetObjectProperty("seen", ParentID, True, False))
            ParentIsSurface = IsYes(GetObjectProperty("surface", ParentID, True, False))
        End If

        For i = 1 To _numberObjs
            ' If object has a parent object
            Parent = GetObjectProperty("parent", i, False, False)

            If Parent <> "" Then

                ' Check if that parent is open, or transparent
                If OnlyParent = "" Then
                    ParentID = GetObjectIDNoAlias(Parent)
                    ParentIsOpen = IsYes(GetObjectProperty("opened", ParentID, True, False))
                    ParentIsTransparent = IsYes(GetObjectProperty("transparent", ParentID, True, False))
                    ParentIsSeen = IsYes(GetObjectProperty("seen", ParentID, True, False))
                    ParentIsSurface = IsYes(GetObjectProperty("surface", ParentID, True, False))
                End If

                If OnlyParent = "" Or (LCase(Parent) = OnlyParent) Then

                    If ParentIsSurface Or ((ParentIsOpen Or ParentIsTransparent) And ParentIsSeen) Then
                        ' If the parent is a surface, then the contents are always available.
                        ' Otherwise, only if the parent has been seen, AND is either open or transparent,
                        ' then the contents are available.

                        SetAvailability(_objs(i).ObjectName, True, ctx)
                    Else
                        SetAvailability(_objs(i).ObjectName, False, ctx)
                    End If

                End If
            End If
        Next i

    End Sub

    Private Function PlayerCanAccessObject(ObjID As Integer, Optional ParentID As Integer = 0, Optional ErrorMsg As String = "", Optional colObjects As List(Of Integer) = Nothing) As Boolean
        ' Called to see if a player can interact with an object (take it, open it etc.).
        ' For example, if the object is on a surface which is inside a closed container,
        ' the object cannot be accessed.

        Dim Parent As String
        Dim ParentDisplayName As String

        Dim sHierarchy As String = ""
        If IsYes(GetObjectProperty("parent", ObjID, True, False)) Then

            ' Object is in a container...

            Parent = GetObjectProperty("parent", ObjID, False, False)
            ParentID = GetObjectIDNoAlias(Parent)

            ' But if it's a surface then it's OK

            If Not IsYes(GetObjectProperty("surface", ParentID, True, False)) And Not IsYes(GetObjectProperty("opened", ParentID, True, False)) Then
                ' Parent has no "opened" property, so it's closed. Hence
                ' object can't be accessed

                If _objs(ParentID).ObjectAlias <> "" Then
                    ParentDisplayName = _objs(ParentID).ObjectAlias
                Else
                    ParentDisplayName = _objs(ParentID).ObjectName
                End If

                ErrorMsg = "inside closed " & ParentDisplayName

                Return False
            End If

            ' Is the parent itself accessible?
            If colObjects Is Nothing Then
                colObjects = New List(Of Integer)
            End If

            If colObjects.Contains(ParentID) Then
                ' We've already encountered this parent while recursively calling
                ' this function - we're in a loop of parents!
                For Each id As Integer In colObjects
                    sHierarchy = sHierarchy & _objs(id).ObjectName & " -> "
                Next
                sHierarchy = sHierarchy & _objs(ParentID).ObjectName
                LogASLError("Looped object parents detected: " & sHierarchy)
                Return False
            End If

            colObjects.Add(ParentID)

            If Not PlayerCanAccessObject(ParentID, , ErrorMsg, colObjects) Then
                Return False
            End If

            Return True
        End If

        Return True
    End Function

    Private Function GetGoToExits(RoomID As Integer, ctx As Context) As String
        Dim i As Integer
        Dim PlaceID As Integer
        Dim PlaceList As String = ""
        Dim ShownPrefix As String
        Dim ShownPlaceName As String

        For i = 1 To _rooms(RoomID).NumberPlaces
            If _gameAslVersion >= 311 And _rooms(RoomID).Places(i).Script = "" Then
                PlaceID = GetRoomID(_rooms(RoomID).Places(i).PlaceName, ctx)
                If PlaceID = 0 Then
                    LogASLError("No such room '" & _rooms(RoomID).Places(i).PlaceName & "'", LogType.WarningError)
                    ShownPlaceName = _rooms(RoomID).Places(i).PlaceName
                Else
                    If _rooms(PlaceID).RoomAlias <> "" Then
                        ShownPlaceName = _rooms(PlaceID).RoomAlias
                    Else
                        ShownPlaceName = _rooms(RoomID).Places(i).PlaceName
                    End If
                End If
            Else
                ShownPlaceName = _rooms(RoomID).Places(i).PlaceName
            End If

            ShownPrefix = _rooms(RoomID).Places(i).Prefix
            If ShownPrefix <> "" Then ShownPrefix = ShownPrefix & " "

            PlaceList = PlaceList & ShownPrefix & "|b" & ShownPlaceName & "|xb, "
        Next i

        Return PlaceList
    End Function

    Private Sub SetUpExits()
        ' Exits have to be set up after all the rooms have been initialised

        Dim i As Integer
        Dim j As Integer
        Dim sRoomName As String
        Dim lRoomID As Integer
        Dim NestedBlock As Integer

        For i = 1 To _numberSections
            If BeginsWith(_lines(_defineBlocks(i).StartLine), "define room ") Then
                sRoomName = RetrieveParameter(_lines(_defineBlocks(i).StartLine), _nullContext)
                lRoomID = GetRoomID(sRoomName, _nullContext)

                For j = _defineBlocks(i).StartLine + 1 To _defineBlocks(i).EndLine - 1
                    If BeginsWith(_lines(j), "define ") Then
                        'skip nested blocks
                        NestedBlock = 1
                        Do
                            j = j + 1
                            If BeginsWith(_lines(j), "define ") Then
                                NestedBlock = NestedBlock + 1
                            ElseIf Trim(_lines(j)) = "end define" Then
                                NestedBlock = NestedBlock - 1
                            End If
                        Loop Until NestedBlock = 0
                    End If

                    _rooms(lRoomID).Exits.AddExitFromTag(_lines(j))
                Next j
            End If
        Next i

        Exit Sub

    End Sub

    Private Function FindExit(sTag As String) As RoomExit
        ' e.g. Takes a tag of the form "room; north" and return's the north exit of room.

        Dim sRoom As String
        Dim sExit As String
        Dim asParams() As String
        Dim lRoomID As Integer
        Dim lDir As Direction

        asParams = Split(sTag, ";")
        If UBound(asParams) < 1 Then
            LogASLError("No exit specified in '" & sTag & "'", LogType.WarningError)
            Return New RoomExit(Me)
        End If

        sRoom = Trim(asParams(0))
        sExit = Trim(asParams(1))

        lRoomID = GetRoomID(sRoom, _nullContext)

        If lRoomID = 0 Then
            LogASLError("Can't find room '" & sRoom & "'", LogType.WarningError)
            Return Nothing
        End If

        Dim exits = _rooms(lRoomID).Exits
        lDir = exits.GetDirectionEnum(sExit)
        If lDir = Direction.None Then
            If exits.Places.ContainsKey(sExit) Then
                Return exits.Places.Item(sExit)
            End If
        Else
            Return exits.GetDirectionExit(lDir)
        End If

        Return Nothing
    End Function

    Private Sub ExecuteLock(sTag As String, bLock As Boolean)
        Dim oExit As RoomExit

        oExit = FindExit(sTag)

        If oExit Is Nothing Then
            LogASLError("Can't find exit '" & sTag & "'", LogType.WarningError)
            Exit Sub
        End If

        oExit.IsLocked = bLock
    End Sub

    Public Sub Begin() Implements IASL.Begin
        Dim runnerThread As New System.Threading.Thread(New System.Threading.ThreadStart(AddressOf DoBegin))
        ChangeState(State.Working)
        runnerThread.Start()

        SyncLock _stateLock
            While _state = State.Working And Not m_gameFinished
                System.Threading.Monitor.Wait(_stateLock)
            End While
        End SyncLock
    End Sub

    Private Sub DoBegin()
        Dim gameblock As DefineBlock = GetDefineBlock("game")
        Dim NewThread As New Context
        Dim i As Integer

        SetFont("", _nullContext)
        SetFontSize(0, _nullContext)

        For i = GetDefineBlock("game").StartLine + 1 To GetDefineBlock("game").EndLine - 1
            If BeginsWith(_lines(i), "background ") Then
                SetBackground(RetrieveParameter(_lines(i), _nullContext))
            End If
        Next i

        For i = GetDefineBlock("game").StartLine + 1 To GetDefineBlock("game").EndLine - 1
            If BeginsWith(_lines(i), "foreground ") Then
                SetForeground(RetrieveParameter(_lines(i), _nullContext))
            End If
        Next i

        ' Execute any startscript command that appears in the
        ' "define game" block:

        _autoIntro = True

        ' For ASL>=391, we only run startscripts if LoadMethod is normal (i.e. we haven't started
        ' from a saved QSG file)

        If _gameAslVersion < 391 Or (_gameAslVersion >= 391 And _gameLoadMethod = "normal") Then

            ' for GameASLVersion 311 and later, any library startscript is executed first:
            If _gameAslVersion >= 311 Then
                ' We go through the game block executing these in reverse order, as
                ' the statements which are included last should be executed first.
                For i = gameblock.EndLine - 1 To gameblock.StartLine + 1 Step -1
                    If BeginsWith(_lines(i), "lib startscript ") Then
                        NewThread = _nullContext
                        ExecuteScript(Trim(GetEverythingAfter(Trim(_lines(i)), "lib startscript ")), NewThread)
                    End If
                Next i
            End If

            For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
                If BeginsWith(_lines(i), "startscript ") Then
                    NewThread = _nullContext
                    ExecuteScript(Trim(GetEverythingAfter(Trim(_lines(i)), "startscript")), NewThread)
                ElseIf BeginsWith(_lines(i), "lib startscript ") And _gameAslVersion < 311 Then
                    NewThread = _nullContext
                    ExecuteScript(Trim(GetEverythingAfter(Trim(_lines(i)), "lib startscript ")), NewThread)
                End If
            Next i

        End If

        _gameFullyLoaded = True

        ' Display intro text
        If _autoIntro And _gameLoadMethod = "normal" Then DisplayTextSection("intro", _nullContext)

        ' Start game from room specified by "start" statement
        Dim StartRoom As String = ""
        For i = gameblock.StartLine + 1 To gameblock.EndLine - 1
            If BeginsWith(_lines(i), "start ") Then
                StartRoom = RetrieveParameter(_lines(i), _nullContext)
            End If
        Next i

        If Not _loadedFromQsg Then
            NewThread = _nullContext
            PlayGame(StartRoom, NewThread)
            Print("", _nullContext)
        Else
            UpdateItems(_nullContext)

            Print("Restored saved game", _nullContext)
            Print("", _nullContext)
            PlayGame(_currentRoom, _nullContext)
            Print("", _nullContext)

            If _gameAslVersion >= 391 Then
                ' For ASL>=391, OnLoad is now run for all games.
                NewThread = _nullContext
                ExecuteScript(_onLoadScript, NewThread)
            End If

        End If

        RaiseNextTimerTickRequest()

        ChangeState(State.Ready)
    End Sub

    Public ReadOnly Property Errors As System.Collections.Generic.List(Of String) Implements IASL.Errors
        Get
            Return New List(Of String)()
        End Get
    End Property

    Public ReadOnly Property Filename As String Implements IASL.Filename
        Get
            Return m_filename
        End Get
    End Property

    Public Sub Finish() Implements IASL.Finish
        GameFinished()
    End Sub

    Public Event Finished() Implements IASL.Finished

    Public Event LogError(errorMessage As String) Implements IASL.LogError

    Public Event PrintText(text As String) Implements IASL.PrintText

    Public Sub Save(filename As String, html As String) Implements IASL.Save
        SaveGame(filename)
    End Sub

    Public Function Save(html As String) As Byte() Implements IASL.Save
        Return SaveGame(Filename, False)
    End Function

    Public ReadOnly Property SaveFilename As String Implements IASL.SaveFilename
        Get
            Return _saveGameFile
        End Get
    End Property

    Public Sub SendCommand(command As String) Implements IASL.SendCommand
        SendCommand(command, 0, Nothing)
    End Sub

    Public Sub SendCommand(command As String, metadata As IDictionary(Of String, String)) Implements IASL.SendCommand
        SendCommand(command, 0, metadata)
    End Sub

    Public Sub SendCommand(command As String, elapsedTime As Integer, metadata As IDictionary(Of String, String)) Implements IASLTimer.SendCommand
        ' The processing of commands is done in a separate thread, so things like the "enter" command can
        ' lock the thread while waiting for further input. After starting to process the command, we wait
        ' for something to happen before returning from the SendCommand call - either the command will have
        ' finished processing, or perhaps a prompt has been printed and now the game is waiting for further
        ' user input after hitting an "enter" script command.

        If Not _readyForCommand Then Exit Sub

        Dim runnerThread As New System.Threading.Thread(New System.Threading.ParameterizedThreadStart(AddressOf ProcessCommandInNewThread))
        ChangeState(State.Working)
        runnerThread.Start(command)

        WaitForStateChange(State.Working)

        If elapsedTime > 0 Then
            Tick(elapsedTime)
        Else
            RaiseNextTimerTickRequest()
        End If

    End Sub

    Private Sub WaitForStateChange(changedFromState As State)
        SyncLock _stateLock
            While _state = changedFromState And Not m_gameFinished
                System.Threading.Monitor.Wait(_stateLock)
            End While
        End SyncLock

    End Sub

    Private Sub ProcessCommandInNewThread(command As Object)
        ' Process command, and change state to Ready if the command finished processing

        Try
            If ExecCommand(DirectCast(command, String), New Context) Then
                ChangeState(State.Ready)
            End If
        Catch ex As Exception
            LogException(ex)
            ChangeState(State.Ready)
        End Try
    End Sub

    Public Sub SendEvent(eventName As String, param As String) Implements IASL.SendEvent

    End Sub

    Public Event UpdateList(listType As ListType, items As System.Collections.Generic.List(Of ListData)) Implements IASL.UpdateList

    Public Function Initialise(player As IPlayer) As Boolean Implements IASL.Initialise
        m_player = player
        If LCase(Right(m_filename, 4)) = ".qsg" Or m_data IsNot Nothing Then
            Return OpenGame(m_filename)
        Else
            Return InitialiseGame(m_filename)
        End If
    End Function

    Private Sub GameFinished()
        m_gameFinished = True
        RaiseEvent Finished()
        ChangeState(State.Finished)

        ' In case we're in the middle of processing an "enter" command, nudge the thread along
        SyncLock _commandLock
            System.Threading.Monitor.PulseAll(_commandLock)
        End SyncLock

        SyncLock _waitLock
            System.Threading.Monitor.PulseAll(_waitLock)
        End SyncLock

        SyncLock _stateLock
            System.Threading.Monitor.PulseAll(_stateLock)
        End SyncLock

        Cleanup()
    End Sub

    Private Function GetResourcePath(filename As String) As String Implements IASL.GetResourcePath
        If Not _resourceFile Is Nothing AndAlso _resourceFile.Length > 0 Then
            Dim extractResult As String = ExtractFile(filename)
            Return extractResult
        End If
        Return System.IO.Path.Combine(_gamePath, filename)
    End Function

    Private Sub Cleanup()
        DeleteDirectory(_tempFolder)
    End Sub

    Private Sub DeleteDirectory(dir As String)
        If System.IO.Directory.Exists(dir) Then
            Try
                System.IO.Directory.Delete(dir, True)
            Catch
            End Try
        End If
    End Sub

    Protected Overrides Sub Finalize()
        Cleanup()
        MyBase.Finalize()
    End Sub

    Private Function GetLibraryLines(libName As String) As String()
        Dim libCode As Byte() = Nothing
        libName = LCase(libName)

        Select Case libName
            Case "stdverbs.lib"
                libCode = My.Resources.stdverbs
            Case "standard.lib"
                libCode = My.Resources.standard
            Case "q3ext.qlb"
                libCode = My.Resources.q3ext
            Case "typelib.qlb"
                libCode = My.Resources.Typelib
            Case "net.lib"
                libCode = My.Resources.net
        End Select

        If libCode Is Nothing Then Return Nothing

        Return GetResourceLines(libCode)
    End Function

    Public ReadOnly Property SaveExtension As String Implements IASL.SaveExtension
        Get
            Return "qsg"
        End Get
    End Property

    Public Sub Tick(elapsedTime As Integer) Implements IASLTimer.Tick
        Dim i As Integer
        Dim TimerScripts As New List(Of String)

        Debug.Print("Tick: " + elapsedTime.ToString)

        For i = 1 To _numberTimers
            If _timers(i).TimerActive Then
                If _timers(i).BypassThisTurn Then
                    ' don't trigger timer during the turn it was first enabled
                    _timers(i).BypassThisTurn = False
                Else
                    _timers(i).TimerTicks = _timers(i).TimerTicks + elapsedTime

                    If _timers(i).TimerTicks >= _timers(i).TimerInterval Then
                        _timers(i).TimerTicks = 0
                        TimerScripts.Add(_timers(i).TimerAction)
                    End If
                End If
            End If
        Next i

        If TimerScripts.Count > 0 Then
            Dim runnerThread As New System.Threading.Thread(New System.Threading.ParameterizedThreadStart(AddressOf RunTimersInNewThread))

            ChangeState(State.Working)
            runnerThread.Start(TimerScripts)
            WaitForStateChange(State.Working)
        End If

        RaiseNextTimerTickRequest()
    End Sub

    Private Sub RunTimersInNewThread(scripts As Object)
        Dim scriptList As List(Of String) = DirectCast(scripts, List(Of String))

        For Each script As String In scriptList
            Try
                ExecuteScript(script, _nullContext)
            Catch ex As Exception
                LogException(ex)
            End Try
        Next

        ChangeState(State.Ready)
    End Sub

    Private Sub RaiseNextTimerTickRequest()
        Dim anyTimerActive As Boolean = False
        Dim nextTrigger As Integer = 60

        For i As Integer = 1 To _numberTimers
            If _timers(i).TimerActive Then
                anyTimerActive = True

                Dim thisNextTrigger As Integer = _timers(i).TimerInterval - _timers(i).TimerTicks
                If thisNextTrigger < nextTrigger Then
                    nextTrigger = thisNextTrigger
                End If
            End If
        Next i

        If Not anyTimerActive Then nextTrigger = 0
        If m_gameFinished Then nextTrigger = 0

        Debug.Print("RaiseNextTimerTickRequest " + nextTrigger.ToString)

        RaiseEvent RequestNextTimerTick(nextTrigger)
    End Sub

    Private Sub ChangeState(newState As State)
        Dim acceptCommands As Boolean = (newState = State.Ready)
        ChangeState(newState, acceptCommands)
    End Sub

    Private Sub ChangeState(newState As State, acceptCommands As Boolean)
        _readyForCommand = acceptCommands
        SyncLock _stateLock
            _state = newState
            System.Threading.Monitor.PulseAll(_stateLock)
        End SyncLock
    End Sub

    Public Sub FinishWait() Implements IASL.FinishWait
        If (_state <> State.Waiting) Then Exit Sub
        Dim runnerThread As New System.Threading.Thread(New System.Threading.ThreadStart(AddressOf FinishWaitInNewThread))
        ChangeState(State.Working)
        runnerThread.Start()
        WaitForStateChange(State.Working)
    End Sub

    Private Sub FinishWaitInNewThread()
        SyncLock _waitLock
            System.Threading.Monitor.PulseAll(_waitLock)
        End SyncLock
    End Sub

    Public Sub FinishPause() Implements IASL.FinishPause
        FinishWait()
    End Sub

    Private m_menuResponse As String

    Private Function ShowMenu(menuData As MenuData) As String
        m_player.ShowMenu(menuData)
        ChangeState(State.Waiting)

        SyncLock _waitLock
            System.Threading.Monitor.Wait(_waitLock)
        End SyncLock

        Return m_menuResponse
    End Function

    Public Sub SetMenuResponse(response As String) Implements IASL.SetMenuResponse
        Dim runnerThread As New System.Threading.Thread(New System.Threading.ParameterizedThreadStart(AddressOf SetMenuResponseInNewThread))
        ChangeState(State.Working)
        runnerThread.Start(response)
        WaitForStateChange(State.Working)
    End Sub

    Private Sub SetMenuResponseInNewThread(response As Object)
        m_menuResponse = DirectCast(response, String)

        SyncLock _waitLock
            System.Threading.Monitor.PulseAll(_waitLock)
        End SyncLock
    End Sub

    Private Sub LogException(ex As Exception)
        RaiseEvent LogError(ex.Message + Environment.NewLine + ex.StackTrace)
    End Sub

    Public Function GetExternalScripts() As IEnumerable(Of String) Implements IASL.GetExternalScripts
        Return Nothing
    End Function

    Public Function GetExternalStylesheets() As IEnumerable(Of String) Implements IASL.GetExternalStylesheets
        Return Nothing
    End Function

    Public Event RequestNextTimerTick(nextTick As Integer) Implements IASLTimer.RequestNextTimerTick

    Public ReadOnly Property OriginalFilename As String Implements IASL.OriginalFilename
        Get
            Return m_originalFilename
        End Get
    End Property

    Private Function GetOriginalFilenameForQSG() As String
        If m_originalFilename IsNot Nothing Then Return m_originalFilename
        Return _gameFileName
    End Function

    Public Delegate Function UnzipFunctionDelegate(filename As String, <Runtime.InteropServices.Out()> ByRef tempDir As String) As String
    Private m_unzipFunction As UnzipFunctionDelegate

    Public Sub SetUnzipFunction(unzipFunction As UnzipFunctionDelegate)
        m_unzipFunction = unzipFunction
    End Sub

    Private Function GetUnzippedFile(filename As String) As String
        Dim tempDir As String = Nothing
        Dim result As String = m_unzipFunction.Invoke(filename, tempDir)
        _tempFolder = tempDir
        Return result
    End Function

    Public Property TempFolder As String Implements IASL.TempFolder
        Get
            Return _tempFolder
        End Get
        Set
            _tempFolder = Value
        End Set
    End Property

    Public ReadOnly Property ASLVersion As Integer
        Get
            Return _gameAslVersion
        End Get
    End Property

    Public Function GetResource(file As String) As System.IO.Stream Implements IASL.GetResource
        If file = "_game.cas" Then
            Return New IO.MemoryStream(GetResourcelessCAS())
        End If
        Dim path As String = GetResourcePath(file)
        If Not System.IO.File.Exists(path) Then Return Nothing
        Return New IO.FileStream(path, IO.FileMode.Open, IO.FileAccess.Read)
    End Function

    Private m_gameId As String

    Public ReadOnly Property GameID() As String Implements IASL.GameID
        Get
            If String.IsNullOrEmpty(_gameFileName) Then Return Nothing
            If Config.ReadGameFileFromAzureBlob Then
                Return m_gameId
            End If
            Return TextAdventures.Utility.Utility.FileMD5Hash(_gameFileName)
        End Get
    End Property

    Public Iterator Function GetResources() As IEnumerable(Of String)
        For i As Integer = 1 To _numResources
            Yield _resources(i).ResourceName
        Next
        If _numResources > 0 Then
            Yield "_game.cas"
        End If
    End Function

    Private Function GetResourcelessCAS() As Byte()
        Dim FileData As String = System.IO.File.ReadAllText(_resourceFile, System.Text.Encoding.GetEncoding(1252))
        Return System.Text.Encoding.GetEncoding(1252).GetBytes(Left(FileData, _startCatPos - 1))
    End Function

End Class