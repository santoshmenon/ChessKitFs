﻿namespace ChessKit.ChessLogic

module internal List = 
    open System.Collections.Generic
    
    // Can replace with List.contains in F# 4.0
    let contains item = List.exists (fun i -> i = item)
    
    // Can replace with List.except in F# 4.0
    let except (itemsToExclude : 'T list) list = 
        let cached = HashSet<'T>(itemsToExclude, HashIdentity.Structural)
        list |> List.filter cached.Add

module internal Operators = 
    open FParsec
    
    let unwrap = 
        function 
        | Success(x, _, _) -> x
        | Failure(x, _, _) -> failwith x

    let getErrorMessage = 
        function 
        | Success(x, _, _) -> failwithf "expected error, but get: %A" x
        | Failure(x, _, _) -> x
    
    let inline (?|?) a b = 
        match a with
        | Some(x) -> x
        | None -> b
