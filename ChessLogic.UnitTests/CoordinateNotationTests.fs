﻿module CoordinateNotationTests

open CoordinateNotation
open Xunit
open FsUnit.Xunit
open Parsing
open Definitions

let positive = 
    ParseToStringShouldMatch ToCoordinateNotation ParseCoordinateNotation
let negative = ErrorMessageShouldMatch ParseCoordinateNotation

[<Fact>]
let ``(4,6) -> (4,4) should read "e2-e4"``() = 
    UsualMove((4, 6), (4, 4))
    |> ToCoordinateNotation
    |> should equal "e2-e4"

[<Fact>]
let ``Promotion move should read correctly``() = 
    PromotionMove({ Vector = ((4, 6), (4, 4))
                    PromoteTo = Queen })
    |> ToCoordinateNotation
    |> should equal "e2-e4=Q"

[<Fact>]
let ``works with e2-e4``() = positive "e2-e4"

[<Fact>]
let ``works with a1-h8``() = positive "a1-h8"

[<Fact>]
let ``works with e2-e4=Q``() = positive "e2-e4=Q"

[<Fact>]
let ``works with a1-h8=Q``() = positive "a1-h8=Q"

[<Fact>]
let ``meaningful error for a1h8``() = negative "a1h8" "Error in Ln: 1 Col: 3
a1h8
  ^
Expecting: '-' or 'x'
"