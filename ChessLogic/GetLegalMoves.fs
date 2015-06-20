﻿[<RequireQualifiedAccess>]
module ChessKit.ChessLogic.GetLegalMoves

open PositionCoreExt

let FromSquare from position = 
    let legalOnly moves = 
        [ for move in moves do
              match move with
              | LegalMove legal -> yield legal
              | _ -> () ]
    
    let rank7 = 1
    let rank2 = 6
    let p (f, t) = Move.Create f t PieceType.Queen
    let u (f, t) = Move.Create f t PieceType.None
    let fromX88 = from |> X88.fromCoordinate
    let validate createMove toX88 = 
        let toCoordinate = toX88 |> Coordinate.fromX88
        let move = createMove (from, toCoordinate)
        position |> MoveLegality.Validate(move)
    let atX88 = position.Core.atX88
    
    let gen createMove = 
        List.map ((+) fromX88)
        >> List.filter (fun x -> (x &&& 0x88) = 0)
        >> List.map (validate createMove)
    
    let rec step start increment = 
        [ let curr = start + increment
          if curr &&& 0x88 = 0 then 
              yield validate u curr
              if atX88 curr = None then yield! step curr increment ]
    
    let iter = List.collect (step fromX88)
    match position.Core.at from with
    | Some(White, PieceType.Pawn) -> 
        if snd from = rank7 then gen p [ -16; -15; -17 ]
        else gen u [ -16; -32; -15; -17 ]
    | Some(Black, PieceType.Pawn) -> 
        if snd from = rank2 then gen p [ +16; +15; +17 ]
        else gen u [ +16; +32; +15; +17 ]
    | Some(_, PieceType.Bishop) -> iter [ -15; +17; +15; -17 ]
    | Some(_, PieceType.Rook) -> iter [ -1; +1; +16; -16 ]
    | Some(_, PieceType.Queen) -> iter [ -15; +17; +15; -17; -1; +1; +16; -16 ]
    | Some(_, PieceType.King) -> gen u [ -15; +17; +15; -17; -1; +1; +16; -16 ]
    | Some(_, PieceType.Knight) -> gen u [ 33; 31; -33; -31; 18; 14; -18; -14 ]
    | None -> []
    | _ -> failwith "unexpected"
    |> legalOnly

let All(position : Position) = 
    [ for i = 0 to 63 do
          let coordinate = Coordinate.fromIdx64 i
          match position.Core.at coordinate with
          | Some(color, _) -> 
              if color = position.Core.ActiveColor then 
                  yield! position |> FromSquare coordinate
          | _ -> () ]
