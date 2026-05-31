// =========================
// Life.fs
// =========================
module ConwayLife.Life

open System
open System.IO

type Cell = int * int
type World = Set<Cell>

let neighbors (x, y) : Cell list =
    [
        (x - 1, y - 1); (x, y - 1); (x + 1, y - 1)
        (x - 1, y);                 (x + 1, y)
        (x - 1, y + 1); (x, y + 1); (x + 1, y + 1)
    ]

let isAlive (world: World) (cell: Cell) : bool =
    Set.contains cell world

let liveNeighborCount (world: World) (cell: Cell) : int =
    neighbors cell
    |> List.sumBy (fun n -> if isAlive world n then 1 else 0)

let candidateCells (world: World) : Set<Cell> =
    world
    |> Seq.collect (fun cell -> seq { yield cell; yield! neighbors cell })
    |> Set.ofSeq

// Бесконечная доска реализована через множество живых клеток.
// Мы не храним массив фиксированного размера, поэтому координаты могут быть любыми:
// ..., -2, -1, 0, 1, 2, ...
let next (world: World) : World =
    candidateCells world
    |> Seq.filter (fun cell ->
        let n = liveNeighborCount world cell
        match isAlive world cell, n with
        | true, 2
        | true, 3
        | false, 3 -> true
        | _ -> false)
    |> Set.ofSeq

let nextN (steps: int) (world: World) : World =
    [1 .. steps] |> List.fold (fun acc _ -> next acc) world

let bounds (world: World) : (int * int * int * int) option =
    if Set.isEmpty world then None
    else
        let xs = world |> Seq.map fst
        let ys = world |> Seq.map snd
        Some (Seq.min xs, Seq.max xs, Seq.min ys, Seq.max ys)

let translate (dx: int, dy: int) (world: World) : World =
    world |> Set.map (fun (x, y) -> x + dx, y + dy)

let union (worlds: World seq) : World =
    worlds |> Seq.fold Set.union Set.empty

let population (world: World) : int =
    Set.count world

let parse (lines: string list) : World =
    lines
    |> List.mapi (fun y line ->
        line
        |> Seq.mapi (fun x ch -> x, y, ch)
        |> Seq.choose (fun (x, y, ch) ->
            match ch with
            | '#' | 'O' | 'X' | '■' | '1' | '*' -> Some (x, y)
            | _ -> None))
    |> Seq.concat
    |> Set.ofSeq

let loadFromFile (path: string) : World =
    File.ReadAllLines(path)
    |> Array.toList
    |> parse

type RleHeader = {
    Width: int option
    Height: int option
    Rule: string option
}

let private tryParseHeaderValue (name: string) (header: string) : string option =
    header.Split(',')
    |> Array.map (fun part -> part.Trim())
    |> Array.tryPick (fun part ->
        let prefix = name + " ="
        if part.StartsWith(prefix) then
            Some(part.Substring(prefix.Length).Trim())
        else
            None)

let parseRleHeader (line: string) : RleHeader =
    let tryInt name =
        line
        |> tryParseHeaderValue name
        |> Option.bind (fun text ->
            match Int32.TryParse text with
            | true, value -> Some value
            | _ -> None)

    {
        Width = tryInt "x"
        Height = tryInt "y"
        Rule = tryParseHeaderValue "rule" line
    }

let parseRle (text: string) : World =
    let lines =
        text.Replace("\r\n", "\n").Split('\n')
        |> Array.toList

    let relevantLines =
        lines
        |> List.map (fun line -> line.Trim())
        |> List.filter (fun line -> line <> "")
        |> List.filter (fun line -> not (line.StartsWith("#")))

    let header, bodyLines =
        match relevantLines with
        | [] -> failwith "RLE file is empty."
        | h :: tail when h.Contains("x") && h.Contains("y") -> parseRleHeader h, tail
        | _ -> failwith "RLE header not found."

    let body = String.Concat(bodyLines)

    let rec loop index x y runCount world =
        if index >= body.Length then
            world
        else
            let ch = body[index]
            if Char.IsDigit ch then
                let mutable j = index
                let mutable value = 0
                while j < body.Length && Char.IsDigit body[j] do
                    value <- value * 10 + int(body[j] - '0')
                    j <- j + 1
                loop j x y value world
            else
                let count = if runCount <= 0 then 1 else runCount
                match ch with
                | 'b' -> loop (index + 1) (x + count) y 0 world
                | 'o' ->
                    let newCells = seq { for dx in 0 .. count - 1 -> (x + dx, y) }
                    let world' = Set.union world (Set.ofSeq newCells)
                    loop (index + 1) (x + count) y 0 world'
                | '$' -> loop (index + 1) 0 (y + count) 0 world
                | '!' -> world
                | _ -> failwithf "Unsupported RLE symbol: %c" ch

    let world = loop 0 0 0 0 Set.empty

    match header.Width, header.Height with
    | Some w, Some h when w >= 0 && h >= 0 ->
        let _ = w, h
        world
    | _ -> world

let loadRleFromFile (path: string) : World =
    File.ReadAllText(path) |> parseRle

let loadAuto (path: string) : World =
    match Path.GetExtension(path).ToLowerInvariant() with
    | ".rle" -> loadRleFromFile path
    | _ -> loadFromFile path

let randomWorld (width: int) (height: int) (aliveProbability: float) (seed: int option) : World =
    if width <= 0 then invalidArg "width" "Width must be positive."
    if height <= 0 then invalidArg "height" "Height must be positive."
    if aliveProbability < 0.0 || aliveProbability > 1.0 then
        invalidArg "aliveProbability" "aliveProbability must be in range [0.0; 1.0]."

    let rng =
        match seed with
        | Some value -> Random(value)
        | None -> Random()

    seq {
        for y in 0 .. height - 1 do
            for x in 0 .. width - 1 do
                if rng.NextDouble() < aliveProbability then
                    yield (x, y)
    }
    |> Set.ofSeq

let toAscii (padding: int) (world: World) : string =
    match bounds world with
    | None -> "<empty>"
    | Some (minX, maxX, minY, maxY) ->
        [
            for y in (minY - padding) .. (maxY + padding) do
                let chars =
                    [
                        for x in (minX - padding) .. (maxX + padding) do
                            if isAlive world (x, y) then '■' else '·'
                    ]
                String(chars |> Array.ofList)
        ]
        |> String.concat "\n"
