#load "references.fsx"
#time "on"


open System
open Akka.Actor
open Akka.FSharp
open FSharp.Json
open WebSocketSharp
open Akka.Configuration

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : OFF
            loglevel : OFF
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 8558
                    hostname = localhost
                }
            }
        }")

let system = ActorSystem.Create("ClientSimulator", configuration)
let echoServer = new WebSocket("ws://localhost:8080/websocket")
echoServer.OnOpen.Add(fun args -> System.Console.WriteLine("Session started!!"))
echoServer.OnClose.Add(fun args -> System.Console.WriteLine("Close"))
echoServer.OnMessage.Add(fun args -> System.Console.WriteLine("Message: {0}", args.Data))
echoServer.OnError.Add(fun args -> System.Console.WriteLine("Error: {0}", args.Message))

echoServer.Connect()

type CommandFormat = {
    Option : string
    Username : string
    Password : string
    UsernameSubs : string
    QueryST : string
    QueryHashtag : string
    QueryMention : string
}

let TwitterClient (mailbox: Actor<string>)=
    let mutable username = ""
    let mutable password = ""

    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        
        let result = message.Split ','
        let operation = result.[0]

        match operation with
        | "Register" ->
            username <- result.[1]
            password <- result.[2]
            let serverJson: CommandFormat = {Option = "register"; Username = result.[1]; Password = result.[2]; UsernameSubs = ""; QueryST = ""; QueryHashtag = ""; QueryMention = ""} 
            let json = Json.serialize serverJson
            echoServer.Send json
            return! loop()  

        | "Login" ->
            username <- result.[1]
            password <- result.[2]
            let serverJson: CommandFormat = {Option = "login"; Username = username; Password = password; UsernameSubs = ""; QueryST = ""; QueryHashtag = ""; QueryMention = ""} 
            let json = Json.serialize serverJson
            echoServer.Send json 

        | "Send" ->
            let serverJson: CommandFormat = {Option = "send"; Username = username; Password = password; UsernameSubs = ""; QueryST = ""+result.[1]; QueryHashtag = ""; QueryMention = ""} 
            let json = Json.serialize serverJson
            echoServer.Send json
            sender <? "success" |> ignore 

        | "Subscribe" ->
            let serverJson: CommandFormat = {Option = "subscribe"; Username = username; Password = password; UsernameSubs = result.[1]; QueryST = ""; QueryHashtag = ""; QueryMention = ""} 
            let json = Json.serialize serverJson
            echoServer.Send json
        
        | "Retweet" ->            
            let serverJson: CommandFormat = {Option = "retweet"; Username = username; Password = password; UsernameSubs = ""; QueryST = result.[1]; QueryHashtag =""; QueryMention =  ""} 
            let json = Json.serialize serverJson
            echoServer.Send json
            sender <? "success" |> ignore
         
        | "QuerySubTweets" ->
            let serverJson: CommandFormat = {Option = "queryST"; Username = username; Password = password; UsernameSubs = ""; QueryST = ""; QueryHashtag = ""; QueryMention = ""} 
            let json = Json.serialize serverJson
            echoServer.Send json
            sender <? "success" |> ignore 
        
        | "QueryHashtags" ->
            let serverJson: CommandFormat = {Option = "queryH"; Username = username; Password = password; UsernameSubs = ""; QueryST = ""; QueryHashtag = result.[1]; QueryMention = ""} 
            let json = Json.serialize serverJson
            echoServer.Send json
            sender <? "success" |> ignore 

        | "QueryMentions" ->            
            let serverJson: CommandFormat = {Option = "queryM"; Username = username; Password = password; UsernameSubs = ""; QueryST = ""; QueryHashtag =""; QueryMention =  result.[1]} 
            let json = Json.serialize serverJson
            echoServer.Send json
            sender <? "success" |> ignore 
        
        | "Logout" ->
            let serverJson: CommandFormat = {Option = "logout"; Username = username; Password = password; UsernameSubs = ""; QueryST = ""; QueryHashtag = ""; QueryMention = ""} 
            let json = Json.serialize serverJson
            echoServer.Send json

        return! loop()     
    }
    loop ()

let client = spawn system ("TwitterClient") TwitterClient
let rec cmdParse () =
    let userInp = Console.ReadLine()
    let inputSubstr = userInp.Split ','
    let cmdOption = inputSubstr.[0]
    
    match (cmdOption) with
    | "register" ->    
        client <! "Register,"+inputSubstr.[1]+","+inputSubstr.[2]
        cmdParse()
    | "login" ->     
        client <! "Login,"+inputSubstr.[1]+","+inputSubstr.[2]
        cmdParse()
    | "send" -> 
        client <! "Send,"+inputSubstr.[1]
        cmdParse()
    | "subscribe" -> 
        client <! "Subscribe,"+inputSubstr.[1]
        cmdParse()
    | "retweet" ->
        client <! "Retweet,"+inputSubstr.[1]
        cmdParse()
    | "queryST" ->
        client <! "QuerySubTweets"
        cmdParse()
    | "queryHashtag" ->
        client <! "QueryHashtags,"+inputSubstr.[1]
        cmdParse()
    | "queryMention" ->
        client <! "QueryMentions,"+inputSubstr.[1]
        cmdParse()
    | "logout" ->
        client <! "Logout"
        cmdParse()
    | "close" ->
        printfn "Session ended!!"
    | _ -> 
        printfn "Bad command!!"
        cmdParse()

cmdParse()

system.Terminate() |> ignore
0 