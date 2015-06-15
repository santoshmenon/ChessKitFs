﻿module FenParserTests

open Xunit
open Parsing
open FenPrinter
open FenParser
open CoordinateNotation
open Definitions
open FsUnit.Xunit
open Option

let positive = ParseToStringShouldMatch ToFen ParseFen
let negative = ErrorMessageShouldMatch ParseFen

[<Fact>]
let works() = positive "p2P3n/8 b KQ - 0 1"

[<Fact>]
let ``works with en-passant``() = positive "p2P3n/8 w KQ e6 0 1"

[<Fact>]
let ``works with no castlings available``() = positive "p2P3n/8 w - e6 0 1"

[<Fact>]
let ``works with starting position``() = 
    positive "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"

[<Fact>]
let ``works with en-passant for black``() = 
    positive "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1"

[<Fact>]
let ``fails nicely with invalid input``() = negative "x w - e3 0 1" "Error in Ln: 1 Col: 1
x w - e3 0 1
^
Expecting: number 1..8 or piece symbol
"

[<Fact>]
let ``starting position print-out looks fine``() = 
    StartingPosition
    |> Dump.Print
    |> should equal (
          " ╔═══╤═══╤═══╤═══╤═══╤═══╤═══╤═══╗\r\n" +
          "8║ r │ n │ b │ q │ k │ b │ n │ r ║\r\n" +
          " ╟───┼───┼───┼───┼───┼───┼───┼───╢\r\n" +
          "7║ p │ p │ p │ p │ p │ p │ p │ p ║\r\n" +
          " ╟───┼───┼───┼───┼───┼───┼───┼───╢\r\n" +
          "6║   │   │   │   │   │   │   │   ║\r\n" +
          " ╟───┼───┼───┼───┼───┼───┼───┼───╢\r\n" +
          "5║   │   │   │   │   │   │   │   ║\r\n" +
          " ╟───┼───┼───┼───┼───┼───┼───┼───╢\r\n" +
          "4║   │   │   │   │   │   │   │   ║\r\n" +
          " ╟───┼───┼───┼───┼───┼───┼───┼───╢\r\n" +
          "3║   │   │   │   │   │   │   │   ║\r\n" +
          " ╟───┼───┼───┼───┼───┼───┼───┼───╢\r\n" +
          "2║ P │ P │ P │ P │ P │ P │ P │ P ║\r\n" +
          " ╟───┼───┼───┼───┼───┼───┼───┼───╢\r\n" +
          "1║ R │ N │ B │ Q │ K │ B │ N │ R ║\r\n" +
          " ╚═══╧═══╧═══╧═══╧═══╧═══╧═══╧═══╝\r\n" +
          "   A   B   C   D   E   F   G   H  \r\n")

[<Fact>]
let ``d1 should refer to Q in starting position``() = 
    StartingPosition.Core
    |> PieceAt (_c "d1") |> get
    |> PieceToString
    |> should equal 'Q'