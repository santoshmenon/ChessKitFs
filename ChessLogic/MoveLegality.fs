﻿[<RequireQualifiedAccess>]
module internal ChessKit.ChessLogic.MoveLegality

open Operators
open Scanning
open PositionCoreExt

let Validate move position = 
    let errors = ref MoveErrors.None
    let observations = ref MoveObservations.None
    let warnings = ref MoveWarnings.None
    let castling = ref Castlings.None
    let newPosition = ref None
    let err e = errors := e ||| !errors
    let warn w = warnings := w ||| !warnings
    let info i = observations := i ||| !observations
    let enPassant() = 
        observations 
        := MoveObservations.Capture ||| MoveObservations.EnPassant ||| !observations
    
    let hasNoEnPassant() = 
        err MoveErrors.HasNoEnPassant
        enPassant()
    
    //   ___________
    //__/ Shortcats \_____________________________________________________
    let moveFrom, moveTo, promoteTo = 
        (move.Start, move.End, 
         if move.PromoteTo = PieceType.None then PieceType.Queen
         else move.PromoteTo)
    
    let positionCore = position.Core
    let at = positionCore.atX88
    let color = positionCore.ActiveColor
    match positionCore.at moveTo with
    | Some(clr, _) when clr = color -> err MoveErrors.ToOccupiedCell
    | Some(_) -> info MoveObservations.Capture
    | None -> ()
    let pieceType : PieceType option = 
        match positionCore.at moveFrom with
        | Some(pieceColor, fPt) -> 
            if color <> pieceColor then err MoveErrors.WrongSideToMove
            Some(fPt)
        | None -> 
            err MoveErrors.EmptyCell
            None
    
    //   _______________________
    //__/ Validateion functions \_________________________________________
    let validatePawnMove fromSquare toSquare = 
        let validateDoublePush v c = 
            if fromSquare / 16 <> c then err MoveErrors.DoesNotMoveThisWay
            else if at toSquare <> None then 
                err MoveErrors.DoesNotCaptureThisWay
            else if at (fromSquare + v) <> None then err MoveErrors.DoesNotJump
            else info MoveObservations.DoublePush
        
        let validatePush c = 
            if at toSquare <> None then err MoveErrors.DoesNotCaptureThisWay
            else 
                if fromSquare / 16 = c then info MoveObservations.Promotion
        
        let validateCapture c2 looksEnPassanty = 
            if at toSquare = None then 
                if looksEnPassanty() then 
                    if positionCore.EnPassant = Some(toSquare % 16) then 
                        enPassant()
                    else hasNoEnPassant()
                else err MoveErrors.OnlyCapturesThisWay
            else 
                if fromSquare / 16 = c2 then info MoveObservations.Promotion
        
        let looksEnPassanty c1 c2 c3 clr () = 
            fromSquare / 16 = c1 && at (fromSquare + c2) = None 
            && at (fromSquare + c3) = (Some(clr, PieceType.Pawn))
        match (color, (toSquare - fromSquare)) with
        | (White, -32) -> validateDoublePush -16 6
        | (Black, +32) -> validateDoublePush +16 1
        | (White, -16) -> validatePush 1
        | (Black, +16) -> validatePush 6
        | (White, -15) -> validateCapture 1 (looksEnPassanty 3 -31 +1 Black)
        | (White, -17) -> validateCapture 1 (looksEnPassanty 3 -33 -1 Black)
        | (Black, +17) -> validateCapture 6 (looksEnPassanty 4 +33 +1 White)
        | (Black, +15) -> validateCapture 6 (looksEnPassanty 4 +31 -1 White)
        | _ -> err MoveErrors.DoesNotMoveThisWay
    
    let validateKnightMove f t = 
        match (t - f) with
        | 33 | 31 | -33 | -31 | 18 | 14 | -18 | -14 -> ()
        | _ -> err MoveErrors.DoesNotMoveThisWay
    
    let validateKingMove fromSquare toSquare = 
        let avail = test positionCore.CastlingAvailability
        
        let long B C D E attacked castlingOpt = 
            if at D <> None || at B <> None then err MoveErrors.DoesNotJump
            else if at C <> None then err MoveErrors.DoesNotCaptureThisWay
            else if not (avail castlingOpt) then err MoveErrors.HasNoCastling
            else if attacked E then err MoveErrors.CastleFromCheck
            else 
                if attacked D then err MoveErrors.CastleThroughCheck
            castling := castlingOpt
        
        let short E F G attacked castlingOpt = 
            if at F <> None then err MoveErrors.DoesNotJump
            else if at G <> None then err MoveErrors.DoesNotCaptureThisWay
            else if not (avail castlingOpt) then err MoveErrors.HasNoCastling
            else if attacked E then err MoveErrors.CastleFromCheck
            else 
                if attacked F then err MoveErrors.CastleThroughCheck
            castling := castlingOpt
        
        let w = position.Core |> IsAttackedBy Black
        let b = position.Core |> IsAttackedBy White
        match (toSquare - fromSquare) with
        | 1 | 15 | 16 | 17 | -1 | -15 | -16 | -17 -> ()
        | -2 | +2 -> 
            match (fromSquare, toSquare) with
            | (E1, C1) -> long B1 C1 D1 E1 w Castlings.WQ
            | (E8, C8) -> long B8 C8 D8 E8 b Castlings.BQ
            | (E1, G1) -> short E1 F1 G1 w Castlings.WK
            | (E8, G8) -> short E8 F8 G8 b Castlings.BK
            | _ -> err MoveErrors.DoesNotMoveThisWay
        | _ -> err MoveErrors.DoesNotMoveThisWay
    
    let validateSlidingMove offsets f t = 
        let rec iterate start stop increment = 
            let next = start + increment
            if next &&& 0x88 <> 0 then err MoveErrors.DoesNotMoveThisWay
            else if next = stop then ()
            else if at next <> None then err MoveErrors.DoesNotJump
            else iterate next stop increment
        
        let isMultipleOf n m = n % m = 0 && n / m < 8 && n / m >= 0
        match offsets |> Seq.tryFind (t - f |> isMultipleOf) with
        | Some(m) -> iterate f t m
        | None -> err MoveErrors.DoesNotMoveThisWay
    
    let validateBishopMove = validateSlidingMove [ 15; -15; 17; -17 ]
    let validateRookMove = validateSlidingMove [ 16; -16; 01; -01 ]
    let validateQueenMove = 
        validateSlidingMove [ 16; -16; 01; -01; 15; -15; 17; -17 ]
    
    let validateByPieceType() = 
        match pieceType.Value with
        | PieceType.Pawn -> validatePawnMove
        | PieceType.Knight -> validateKnightMove
        | PieceType.King -> validateKingMove
        | PieceType.Bishop -> validateBishopMove
        | PieceType.Rook -> validateRookMove
        | PieceType.Queen -> validateQueenMove
        | _ -> failwith "unexpected"
    
    //   _______
    //__/ Steps \_________________________________________________________
    let validate() = 
        validateByPieceType () (X88.fromCoordinate moveFrom) 
            (X88.fromCoordinate moveTo)
    
    let setupResultPosition() = 
        let newPlacement = Array.copy positionCore.Placement
        // Remove the pawn captured en-passant
        if !observations |> test MoveObservations.EnPassant then 
            let increment = 
                if color = White then +8
                else -8
            newPlacement.[(moveTo |> Coordinate.toIdx64) + increment] <- None
        // Remove the piece from the old square and put it to the new square
        let effectivePieceType = 
            if !observations |> test MoveObservations.Promotion then promoteTo
            else pieceType.Value
        
        let effectivePiece = Some((color, effectivePieceType))
        newPlacement.[moveTo |> Coordinate.toIdx64] <- effectivePiece
        newPlacement.[moveFrom |> Coordinate.toIdx64] <- None
        // Move the rook if it was a castling
        let moveCastlingRook f t = 
            let rook = newPlacement.[f |> X88.toIdx64]
            newPlacement.[f |> X88.toIdx64] <- None
            newPlacement.[t |> X88.toIdx64] <- rook
        match !castling with
        | Castlings.WK -> moveCastlingRook H1 F1
        | Castlings.WQ -> moveCastlingRook A1 D1
        | Castlings.BK -> moveCastlingRook H8 F8
        | Castlings.BQ -> moveCastlingRook A8 D8
        | _ -> ()
        // Figure out new castling availability
        let optionsInvalidatedBy p = 
            match p |> X88.fromCoordinate with
            | A1 -> Castlings.WQ
            | E1 -> Castlings.W
            | H1 -> Castlings.WK
            | A8 -> Castlings.BQ
            | E8 -> Castlings.B
            | H8 -> Castlings.BK
            | _ -> Castlings.None
        
        let newCastlingAvailability = 
            positionCore.CastlingAvailability 
            &&& ~~~((optionsInvalidatedBy moveFrom) 
                    ||| (optionsInvalidatedBy moveTo))
        
        // Figure out new en-passant option
        let newEnPassant = 
            if !observations |> test MoveObservations.DoublePush then 
                Some(fst moveFrom)
            else None
        
        // Construct new position
        let updatedPosition = 
            { positionCore with Placement = newPlacement
                                ActiveColor = color.Invert
                                EnPassant = newEnPassant
                                CastlingAvailability = newCastlingAvailability }
        
        newPosition := Some(updatedPosition)
    
    let setMoveToCheck() = 
        if IsInCheck color (!newPosition).Value then 
            err MoveErrors.MoveToCheck
            newPosition := None
    
    let setRequiresPromotion() = 
        let requiresPromotion = !observations |> test MoveObservations.Promotion
        if move.PromoteTo = PieceType.None then 
            if requiresPromotion then warn MoveWarnings.MissingPromotionHint
        else 
            if not requiresPromotion then 
                warn MoveWarnings.PromotionHintIsNotNeeded
    
    //   __________
    //__/ Do steps \______________________________________________________    
    List.iter (fun f -> 
        if !errors = MoveErrors.None then f()) 
        [ validate; setupResultPosition; setMoveToCheck; setRequiresPromotion ]
    if !errors = MoveErrors.None then 
        LegalMove { Move = move
                    OriginalPosition = position
                    ResultPosition = (!newPosition).Value
                    Piece = pieceType.Value
                    Castling = !castling
                    Observations = !observations
                    Warnings = !warnings }
    else 
        IllegalMove { Move = move
                      OriginalPosition = position
                      Piece = pieceType
                      Castling = !castling
                      Observations = !observations
                      Warnings = !warnings
                      Errors = !errors }

let ValidateLegal move pos = 
    match Validate move pos with
    | LegalMove m -> m
    | IllegalMove(_) -> failwith "move is illegal"

let ParseLegal move pos = ValidateLegal (Move.Parse move) pos
