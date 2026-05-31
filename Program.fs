open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open ConwayLife.Life

module LifeBridge =
    let boardWidth  = 40
    let boardHeight = 25
    let defaultProbability = 0.25

    let mutable offsetX = 0
    let mutable offsetY = 0
    let mutable generation = 0

    let mutable world : World =
        randomWorld boardWidth boardHeight defaultProbability None
        |> translate (offsetX, offsetY)

    let visibleCells () =
        [|
            for y in 0 .. boardHeight - 1 do
                for x in 0 .. boardWidth - 1 do
                    if isAlive world (offsetX + x, offsetY + y) then 1 else 0
        |]

    let getState () =
        {| width = boardWidth
           height = boardHeight
           offsetX = offsetX
           offsetY = offsetY
           generation = generation
           population = population world
           cells = visibleCells() |}

    let step () =
        world <- next world
        generation <- generation + 1

    let clear () =
        world <- Set.empty
        generation <- 0

    let randomize probability =
        let p =
            if probability < 0.0 then 0.0
            elif probability > 1.0 then 1.0
            else probability

        world <-
            randomWorld boardWidth boardHeight p None
            |> translate (offsetX, offsetY)

        generation <- 0

    let toggleVisibleCell x y =
        if x >= 0 && x < boardWidth && y >= 0 && y < boardHeight then
            let cell = offsetX + x, offsetY + y
            if isAlive world cell then
                world <- Set.remove cell world
            else
                world <- Set.add cell world

    let setVisibleCell x y alive =
        if x >= 0 && x < boardWidth && y >= 0 && y < boardHeight then
            let cell = offsetX + x, offsetY + y
            if alive then
                world <- Set.add cell world
            else
                world <- Set.remove cell world

let builder = WebApplication.CreateBuilder()
builder.Services.AddRouting() |> ignore

let app = builder.Build()

app.UseDefaultFiles() |> ignore
app.UseStaticFiles() |> ignore

app.MapGet("/api/state", Func<IResult>(fun () ->
    Results.Json(LifeBridge.getState())
)) |> ignore

app.MapPost("/api/step", Func<IResult>(fun () ->
    LifeBridge.step()
    Results.Json(LifeBridge.getState())
)) |> ignore

app.MapPost("/api/clear", Func<IResult>(fun () ->
    LifeBridge.clear()
    Results.Json(LifeBridge.getState())
)) |> ignore

app.MapPost("/api/random", Func<IResult>(fun () ->
    LifeBridge.randomize 0.25
    Results.Json(LifeBridge.getState())
)) |> ignore

app.MapPost("/api/toggle/{x}/{y}", Func<int, int, IResult>(fun x y ->
    LifeBridge.toggleVisibleCell x y
    Results.Json(LifeBridge.getState())
)) |> ignore

app.MapPost("/api/alive/{x}/{y}", Func<int, int, IResult>(fun x y ->
    LifeBridge.setVisibleCell x y true
    Results.Json(LifeBridge.getState())
)) |> ignore

app.Run("http://127.0.0.1:5000")