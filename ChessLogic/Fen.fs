﻿[<RequireQualifiedAccess>]
module ChessKit.ChessLogic.Fen

open Text
open System
open System.Text
open Operators

let Print p = 
    let color = 
        match p.Core.ActiveColor with
        | White -> "w"
        | Black -> "b"
    
    let options = 
        [ (Castlings.WK, 'K');
          (Castlings.WQ, 'Q');
          (Castlings.BK, 'k');
          (Castlings.BQ, 'q') ]

    let castling = 
        let result = 
            [| for o in options do
                   if fst o |> test p.Core.CastlingAvailability then
                       yield (snd o) |]
            // This can be just "String" in F# 4.0
            |> (fun arr -> (String(arr)))
        if result = "" then "-"
        else result
    
    let enPassant = 
        match p.Core.EnPassant with
        | Some(file) -> 
            (fileToStirng file) + (if p.Core.ActiveColor = Black then "3"
                                   else "6")
        | None -> "-"
    
    let sb = new StringBuilder()
    let appendc (c : char) = sb.Append(c) |> ignore
    let appends (s : string) = sb.Append(s) |> ignore
    let appendi (i : int) = sb.Append(i) |> ignore
    let mutable skip = 0
    let mutable file = 0
    for square in p.Core.Placement do
        if file = 8 then 
            appendc '/'
            file <- 0
        file <- file + 1
        match square with
        | Some p -> 
            if skip > 0 then 
                appendi skip
                skip <- 0
            appendc (pieceToChar p)
        | None -> skip <- skip + 1
        if file = 8 && skip > 0 then 
            appendi skip
            skip <- 0
    appendc ' '
    appends color
    appendc ' '
    appends castling
    appendc ' '
    appends enPassant
    appendc ' '
    appendi p.HalfMoveClock
    appendc ' '
    appendi p.FullMoveNumber
    string sb

open FParsec
open Microsoft.FSharp.Core.Option

type private Code = 
    | Piece of Piece
    | Gap of int

let TryParse str = 
    let parsePieceLetter = 
        function 
        | 'P' -> (White, PieceType.Pawn)
        | 'N' -> (White, PieceType.Knight)
        | 'B' -> (White, PieceType.Bishop)
        | 'R' -> (White, PieceType.Rook)
        | 'Q' -> (White, PieceType.Queen)
        | 'K' -> (White, PieceType.King)
        | 'p' -> (Black, PieceType.Pawn)
        | 'n' -> (Black, PieceType.Knight)
        | 'b' -> (Black, PieceType.Bishop)
        | 'r' -> (Black, PieceType.Rook)
        | 'q' -> (Black, PieceType.Queen)
        | 'k' -> (Black, PieceType.King)
        | _ -> failwith ("unknown piece letter")
    
    let parseGap c = Gap(int c - int '0')
    let parsePiece c = Piece(parsePieceLetter c)
    let fold = List.fold (|||) Castlings.None

    let castlingHintParse = 
        function 
        | 'Q' -> Castlings.WQ
        | 'K' -> Castlings.WK
        | 'q' -> Castlings.BQ
        | 'k' -> Castlings.BK
        | _ -> failwith "unknown castling symbol"
    
    let parsePlacement ranks = 
        [| for rank in ranks do
               for square in rank do
                   match square with
                   | Piece(p) -> yield Some(p)
                   | Gap(n) -> for _ in 1..n -> None |]
    
    let createCore plcmnt clr ca enp = 
        { Placement = parsePlacement (plcmnt)
          ActiveColor = clr
          CastlingAvailability = ca
          EnPassant = map fst enp }
    
    let createPosition core halfMoveClock fullMoveNumber = 
        { Core = core
          HalfMoveClock = halfMoveClock
          FullMoveNumber = fullMoveNumber
          Properties = Properties.None
          Move = None }
    
    let piece = anyOf "pnbrqkPNBRQK" |>> parsePiece <?> "piece symbol"
    let gap = anyOf "12345678" |>> parseGap <?> "number 1..8"
    let rank = many1 (piece <|> gap)
    let ws = pchar ' '
    let ranks = sepBy1 rank (pchar '/') .>> ws
    let black = pchar 'b' >>% Black
    let white = pchar 'w' >>% White
    let color = black <|> white .>> ws
    let castlingHint = anyOf "KQkq" |>> castlingHintParse
    let castlings = many1 castlingHint |>> fold
    let noCastling = pchar '-' >>% Castlings.None
    let ca = noCastling <|> castlings .>> ws
    let file = anyOf "abcdefgh" |>> parseFile
    let enColor = (pchar '3' >>% Black) <|> (pchar '6' >>% White)
    let en = (file .>>. enColor) |>> Some
    let enPassant = ((pchar '-' >>% None) <|> en) .>> ws
    let core = pipe4 ranks color ca enPassant createCore
    let n = pint32
    let fenParser = pipe3 core (n .>> ws) n createPosition
    run fenParser str

let Parse str = TryParse str |> Operators.getSuccess
let ParseCore str = (Parse str).Core

