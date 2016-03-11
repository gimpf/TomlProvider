#load "load-project-debug.fsx"

(*
#I __SOURCE_DIRECTORY__
#I "..\\..\\bin\\Debug"
#load "load-references-debug.fsx"
#r "TomlProvider.dll"
open Gimpf.Toml

// --------------------------------------------------------

[<Literal>] let exSingle = """Entry = 1"""

let tmlSingle = Nett.Toml.ReadString exSingle
type TSingle = TomlDoc<exSingle>
let tSingle = TSingle(exSingle)
let expected = 1
let actual = tSingle.Entry
printfn "Expected: %A" expected
printfn "Actual: %A" actual

// --------------------------------------------------------

[<Literal>] let exSimple = """
[Header]
Entry = 15
"""

let tmlSimple = Nett.Toml.ReadString exSimple
type TSimple = TomlDoc<exSimple>
let tSimple = TSimple(exSimple)
let expected = 15
let actual = tSimple.Header.Entry
printfn "Expected: %A" expected
printfn "Actual: %A" actual

// --------------------------------------------------------

[<Literal>] let exRegular = """
title = "TOML Example"

[owner]
name = "Tom Preston-Werner"
dob = 1979-05-27T07:32:00-08:00

[database]
server = "192.168.1.1"
ports = [ "8001", "8001", "8002" ]
"""

let tmlRegular = Nett.Toml.ReadString exRegular
type TRegular = TomlDoc<exRegular>
let tRegular = TRegular(exRegular)
let expected = "8001"
let actual = tRegular.database.ports.[0]
printfn "Expected: %A" expected
printfn "Actual: %A" actual

// --------------------------------------------------------

[<Literal>]
let exTableArray = """
[Header]
Entry = 123.3

[OtherHeader]
OtherEntry = "Guck mal."

[[SuppaDuppaTable]]
Entry = 1

[[SuppaDuppaTable]]
Entry = 2
ArrayEntry = [ 1, 2, 3, 4]
"""

let tmlTableArray = Nett.Toml.ReadString exTableArray
type TTableArray = TomlDoc<exTableArray>
let tTableArray = TTableArray(exTableArray)
let expected = 1
let actual = tTableArray.SuppaDuppaTable.[0].Entry
printfn "Expected: %A" expected
printfn "Actual: %A" actual
let expected = 4
// TODO this one needs improving
let actual = tTableArray.SuppaDuppaTable.[1].Rows.["ArrayEntry"] :?> Nett.TomlArray).[3]
printfn "Expected: %A" expected
printfn "Actual: %A" actual

*)
