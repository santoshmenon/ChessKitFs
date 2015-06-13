﻿module San

open Definitions
open MoveLegalityChecker
open System.Text
open CoordinateNotation
open FParsec
open IsAttackedBy

let ToSanString(move : LegalMove) = 
    //     _______________________
    // ___/ Shortcuts and helpers \___________________________
    let typeToString = 
        function 
        | Pawn -> 'P'
        | Knight -> 'N'
        | Bishop -> 'B'
        | Rook -> 'R'
        | Queen -> 'Q'
        | King -> 'K'
    
    let sb = new StringBuilder(6)
    let mv = move.Move
    let shortCastling = move.Castling = Some(WK) || move.Castling = Some(BK)
    let longCastling = move.Castling = Some(WQ) || move.Castling = Some(BQ)
    let capture = move.Observations |> MyList.contains Capture
    let promotion = move.Observations |> MyList.contains Promotion
    let check = move.ResultPosition.Observations |> MyList.contains Check
    let mate = move.ResultPosition.Observations |> MyList.contains Mate
    let append (str : string) = sb.Append(str) |> ignore
    let appendc (str : char) = sb.Append(str) |> ignore
    let file, rank, fileAndRankStr = fst, snd, CoordinateToString
    let fileStr x = fileToStirng (x |> file)
    let rankStr x = rankToString (x |> rank)
    let at x = move.OriginalPosition |> PieceAt x
    let isSimilarTo (a:LegalMove) (b:LegalMove) = 
        let x, y = a.Move, b.Move
        (x.Start <> y.Start) && (x.End = y.End) && (at x.Start = at y.Start)
    
    let disambiguationList = 
        lazy ([ for m in move.OriginalPosition |> GetLegalMoves.All do
                    if m |> isSimilarTo move then yield m.Move.Start ])
    let ambiguous() = not (disambiguationList.Value |> List.isEmpty)
    let unique fn = 
        disambiguationList.Value 
        |> List.forall (fun x -> (mv.Start |> fn) <> (x |> fn))
    //     __________________
    // ___/ Actual algorithm \________________________________
    if shortCastling then append "O-O"
    else if longCastling then append "O-O-O"
    else 
        if move.Piece = Pawn then 
            if capture then append (fileStr mv.Start)
        else 
            appendc (move.Piece |> typeToString)
            if ambiguous() then 
                if unique file then append (fileStr mv.Start)
                else if unique rank then append (rankStr mv.Start)
                else append (fileAndRankStr mv.Start)
        if capture then appendc 'x'
        append (fileAndRankStr mv.End)
    if promotion then 
        appendc '='
        appendc (mv.PromoteTo.Value |> typeToString)
    if check then appendc '+'
    else if mate then appendc '#'
    string sb

type Ending = 
    | SanCheck
    | SanMate

type SanCapture = 
    | SanCapture

type Hint = 
    | FileHint of File
    | RankHint of Rank
    | SquareHint of Coordinate
    | NoHint

type Moves = 
    | ShortCastling
    | LongCastling
    | PawnPush of Coordinate * PieceType option
    | PawnCapture of File * (Coordinate * PieceType option)
    | Usual of (PieceType * (Hint * (SanCapture option * Coordinate)))

let ParseSanString str = 
    let parseFile = LetterToFileNoCheck
    let parseRank (c : char) : Rank = (int '8') - (int c)
    
    let parsePiece = 
        function 
        | 'N' -> Knight
        | 'B' -> Bishop
        | 'R' -> Rook
        | 'Q' -> Queen
        | 'K' -> Queen
        | _ -> failwith ("unknown promotion hint")
    
    let short = stringReturn "O-O" ShortCastling
    let long = stringReturn "O-O-O" LongCastling
    let check = charReturn '+' SanCheck
    let mate = charReturn '#' SanMate
    let file = anyOf "abcdefgh" |>> parseFile
    let rank = anyOf "12345678" |>> parseRank
    let piece = anyOf "NBRQK" |>> parsePiece
    let capture = anyOf "x:" >>% SanCapture
    let promotion = skipChar '=' >>. anyOf "NBRQ" |>> parsePiece
    let ending = check <|> mate
    let square = file .>>. rank
    let pawn = square .>>. opt promotion
    let pawnPush = pawn |>> PawnPush
    let pawnCapture = attempt (file .>> capture .>>. pawn) |>> PawnCapture
    let target = opt capture .>>. square
    let squareHint = attempt square |>> SquareHint
    let fileHint = file |>> FileHint
    let rankHint = rank |>> RankHint
    let hint = choice [ squareHint; fileHint; rankHint ]
    let hinted = attempt (hint .>>. target)
    let hintless = preturn NoHint .>>. target
    let move = piece .>>. (hinted <|> hintless) |>> Usual
    let moves = choice [ long; short; move; pawnCapture; pawnPush ]
    let san = moves .>>. opt ending
    run san str

type SanError = 
    | PieceNotFound of Piece
    | AmbiguousChoice of LegalMove list
    | ChoiceOfIllegalMoves of IllegalMove list

type SanWarning = 
    | IsCapture
    | IsNotCapture
    | IsCheck
    | IsNotCheck
    | IsMate
    | IsNotMate
    | DisambiguationIsExcessive

type SanMove = 
    | LegalSan of LegalMove * SanWarning list
    | IllegalSan of IllegalMove 
    | Nonsense of SanError
    | Unparsable of string

let sanScanners board = 
    let at88 i = board |> PieceAt(i |> fromX88)
    let color = board.ActiveColor
    let project = 
        Seq.map (fun f -> f())
        >> Seq.filter (fun x -> x <> -1)
        >> Seq.map fromX88
        >> Seq.toList

    let findPushingPawns square = 
        let _, slide = getScanners color at88 square
        match color with
        | Black -> slide Pawn [ -16; ]
        | White -> slide Pawn [ +16; ]
        |> project

    let findCapturingPawns square = 
        let jump, _ = getScanners color at88 square
        match color with
        | Black -> jump Pawn [ -15; -17 ]
        | White -> jump Pawn [ +15; +17 ]
        |> project

    let findNonPawnPieces ofType square = 
        let jump, slide = getScanners color at88 square
        match ofType with
        | Knight -> jump Knight [ -33; -31; -18; -14; +33; +31; +18; +14 ]
        | Queen -> slide Queen [ +15; +17; -15; -17; +16; +01; -16; -01 ]
        | Rook -> slide Rook [ +16; +01; -16; -01 ]
        | Bishop -> slide Bishop [ +15; +17; -15; -17 ]
        | King -> jump King [ +15; +17; -15; -17; +16; +01; -16; -01 ]
        | Pawn -> failwith "unexpected"
        |> project
    (findPushingPawns, findCapturingPawns, findNonPawnPieces)

let FromSanString str board = 
    let color = board.ActiveColor
    let findPushingPawns, findCapturingPawns, findNonPawnPieces = 
        sanScanners board
    
    let addNotesToLegal notes capture warns legalMove =
        let warnings = ref warns
        let warn w = warnings := w :: !warnings

        let checkNote = notes = Some(SanCheck)
        let checkReal = legalMove.ResultPosition.Observations |> MyList.contains Check
        if not checkNote && checkReal then warn IsCheck
        else if checkNote && not checkReal then warn IsNotCheck
                    
        let mateNote = notes = Some(SanMate)
        let mateReal = legalMove.ResultPosition.Observations |> MyList.contains Mate
        if not mateNote && mateReal then warn IsMate
        else if mateNote && not mateReal then warn IsNotMate
                    
        let captureNote = capture = Some(SanCapture)
        let captureReal = legalMove.Observations |> MyList.contains Capture
        if not captureNote && captureReal then warn IsCapture
        else if captureNote && not captureReal then warn IsNotCapture
            
        LegalSan(legalMove, !warnings)

    let addNotesToAny notes capture warns moveInfo =
        match moveInfo with
        | LegalMove m -> addNotesToLegal notes capture warns m
        | IllegalMove m -> IllegalSan m

    let castlingToSanMove opt notes = 
        let move = 
            match color, opt with
            | White, ShortCastling -> "e1-g1"
            | White, LongCastling -> "e1-c1"
            | Black, ShortCastling -> "e8-g8"
            | Black, LongCastling -> "e8-c8"
            | _ -> failwith "unexpected"
        board 
        |> ValidateMove(_cn (move))
        |> addNotesToAny notes None []
    
    let validate promoteTo fromSquare toSquare = 
        ValidateMove (Move.Create fromSquare toSquare promoteTo) board

    let disambiguate hint moves = 
        let unique (m : IMoveSource) = 
            match hint with
            | FileHint f -> m.Move.Start |> fst = f
            | RankHint r -> m.Move.Start |> snd = r
            | SquareHint s -> m.Move.Start = s
            | NoHint -> true
        moves |> List.filter unique

    let findAndSeparate find toSquare validate () = 
        let separateToLegalAndIllegal list =
            let mutable valid = []
            let mutable invalid = []
            for move in list do
                match move with
                | LegalMove m -> valid <- m::valid
                | IllegalMove m -> invalid <- m::invalid
            (valid, invalid)

        find (toSquare |> toX88)
        |> List.map (fun x -> validate x toSquare)
        |> separateToLegalAndIllegal

    let toSanMove find hint pieceType addNotes = 
        let validCandidates, invalidCandidates = find()
        let valid = disambiguate hint validCandidates
        let invalid = disambiguate hint invalidCandidates

        let warnings = 
            if validCandidates |> List.length = 1 && hint <> NoHint then 
                [ DisambiguationIsExcessive ]
            else []

        match valid, invalid with
        | [], _::_::[] -> Nonsense (ChoiceOfIllegalMoves invalid)
        | [], only::[] -> IllegalSan only
        | [], [] -> Nonsense (PieceNotFound (color, pieceType))
        | validMove::[], _ -> validMove |> addNotes warnings
        | tooMany, _ -> Nonsense (AmbiguousChoice tooMany)
 
    let dispatch = 
        function 
        | ShortCastling, notes -> castlingToSanMove ShortCastling notes
        | LongCastling, notes -> castlingToSanMove LongCastling notes
        | PawnPush(toSquare, promoteTo), notes -> 
            let addNotes = addNotesToLegal notes None
            let find = findAndSeparate findPushingPawns toSquare (validate promoteTo) 
            toSanMove find NoHint Pawn addNotes

        | PawnCapture(fromFile, (toSquare, promoteTo)), notes -> 
            let addNotes = addNotesToLegal notes (Some(SanCapture))
            let find = findAndSeparate findCapturingPawns toSquare (validate promoteTo) 
            toSanMove find (FileHint fromFile) Pawn addNotes

        | Usual(pieceType, (hint, (capture, toSquare))), notes -> 
            let addNotes = addNotesToLegal notes capture
            let find = findAndSeparate (findNonPawnPieces pieceType) toSquare (validate None)
            toSanMove find hint pieceType addNotes
           
    match ParseSanString str with
    | Success(p, _, _) -> dispatch p
    | Failure(e, _, _) -> Unparsable e

let FromLegalSanString str board = 
    match FromSanString str board with
    | LegalSan (move, _) -> move
    | x -> failwithf "%A" x
