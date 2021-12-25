open System
open Akka.Actor
open Akka.FSharp

open FSharp.Json
open Suave
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.RequestErrors
open Suave.Logging
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Newtonsoft.Json
open Suave.Writers

type UserCommands =
    | CmdRegister of string* string * WebSocket
    | CmdLogin of string * string * WebSocket
    | CmdSend of  string  * string* string* bool
    | CmdSubscribe of   string  * string* string 
    | CmdRetweet of  string  * string * string
    | CmdQueryST of  string  * string 
    | CmdQueryHashtag of   string   
    | CmdQueryMention of   string  
    | CmdLogout of  string* string

type ServerReply = {
    Status : string
    Data : string
}

let system = ActorSystem.Create("Twitter")

type Tweet(tweetID:string, tweetText:string, isRetweet:bool) =
    member this.tweetID = tweetID
    member this.tweetText = tweetText
    member this.IsReTweet = isRetweet
    override this.ToString() =
      let mutable tempVar = ""
      if isRetweet then
        tempVar <- sprintf "Retweet!! [%s]%s" this.tweetID this.tweetText
      else
        tempVar <- sprintf "[%s]%s" this.tweetID this.tweetText
      tempVar

type TwitterUser(username:string, password:string, webSocket:WebSocket) =
    let mutable tweetsList = List.empty: Tweet list
    let mutable subscribedTo = List.empty: TwitterUser list
    let mutable subscribedBy = List.empty: TwitterUser list
    let mutable socket = webSocket
    let mutable loginStatus = true
    member this.username = username
    member this.password = password
    member this.newTweet x =
        tweetsList <- List.append tweetsList [x]
    member this.MyTweets() =
        tweetsList
    member this.Subscribed() =
        subscribedTo
    member this.Subscribers() =
        subscribedBy
    member this.newSubscribe user =
        subscribedTo <- List.append subscribedTo [user]
    member this.newSubscriber user =
        subscribedBy <- List.append subscribedBy [user]
    member this.MySocket() =
        socket
    member this.SetSocket(webSocket) =
        socket <- webSocket
    member this.Login() = 
        loginStatus
    member this.Logout() =
        loginStatus <- false
    override this.ToString() = 
        this.username

type TwitterEngine() =
    let mutable tweets = new Map<string,Tweet>([])
    let mutable hashtags = new Map<string, Tweet list>([])
    let mutable mentions = new Map<string, Tweet list>([])
    let mutable users = new Map<string,TwitterUser>([])    
    
    member this.newTweet (tweet:Tweet) =
        tweets <- tweets.Add(tweet.tweetID,tweet)

    member this.newHashTag hashtag tweet =
        let key = hashtag
        let mutable hashmap = hashtags
        if hashmap.ContainsKey(key) = false then
            let tempList = List.empty: Tweet list
            hashmap <- hashmap.Add(key, tempList)
        let value = hashmap.[key]
        hashmap <- hashmap.Add(key, List.append value [tweet])
        hashtags <- hashmap

    member this.newMention mention tweet = 
        let key = mention
        let mutable hashmap = mentions
        if hashmap.ContainsKey(key) = false then
            let tempList = List.empty: Tweet list
            hashmap <- hashmap.Add(key, tempList)
        let value = hashmap.[key]
        hashmap <- hashmap.Add(key, List.append value [tweet])
        mentions <- hashmap

    member this.AllTweets() = 
        tweets

    member this.newUser (user:TwitterUser) =
        users <- users.Add(user.username, user)

    member this.UserDetails() = 
        users

    member this.RegisterUser username password webSocket=
        let mutable tempVar:ServerReply = {Status=""; Data=""}
        if not (users.ContainsKey(username)) then
            let user = TwitterUser(username, password, webSocket)
            this.newUser user
            user.newSubscribe user
            tempVar<-{Status="Success"; Data="New User!! "+username}
        else
            tempVar<-{Status="Error"; Data="Username already exists!!"}
        tempVar
    
    member this.Login username password webSocket=
        let mutable tempVar :ServerReply = {Status=""; Data=""}
        if users.ContainsKey(username) then
            let user = users.[username]
            if user.password = password then
                user.SetSocket(webSocket)
                tempVar <- {Status="Success"; Data="User is logged in!!"}
            else
                tempVar <- {Status="Error"; Data="Wrong details!!"}
        else
            printfn "%s" "Username not found!!"
            tempVar <- {Status="Error"; Data="Username not found!!"}
        tempVar
    
    member this.userDetails username = 
        let mutable tempVar : TwitterUser = Unchecked.defaultof<TwitterUser>
        if users.ContainsKey(username) then
            tempVar <- users.[username]
        else
            printfn "%s" "Username not found!!"            
        tempVar
    
    member this.CheckDetails username password =
        let mutable tempVar = false
        if users.ContainsKey(username) then
            let user = users.[username]
            if user.password = password then
                tempVar <- true
        else
            printfn "%s" "Username not found!!"            
        tempVar

    member this.SendTweet username password text isRetweet =
        let mutable tempVar:ServerReply = {Status=""; Data=""}
        if this.CheckDetails username password then
            if users.ContainsKey(username) then
                let user = users.[username]
                let tweet = Tweet(DateTime.Now.ToFileTimeUtc() |> string, text, isRetweet)
                user.newTweet tweet
                this.newTweet tweet
         
                let hidx1 = text.IndexOf("#")
                if hidx1 <> -1 then
                    let mutable hidx2 = text.IndexOf(" ",hidx1)
                    if hidx2 = -1 then
                        hidx2 <- text.Length
                    let hashtag = text.[hidx1..hidx2-1]
                    this.newHashTag hashtag tweet

                let midx1 = text.IndexOf("@")
                if midx1 <> -1 then
                    let mutable midx2 = text.IndexOf(" ",midx1)
                    if midx2 = -1 then
                        midx2 <- text.Length
                    let mention = text.[midx1..midx2-1]
                    this.newMention mention tweet
                
                tempVar<-{Status="Success"; Data="Tweet sent!! "+tweet.ToString()}
            else                
                tempVar<-{Status="Error"; Data="Username not found!!"}
        else            
            tempVar<-{Status="Error"; Data="Wrong details!!"}
        tempVar

    member this.subscribe username1 password username2 =
        let mutable tempVar:ServerReply = {Status=""; Data=""}
        if this.CheckDetails username1 password then
            let user1 = this.userDetails username1
            let user2 = this.userDetails username2
            user1.newSubscribe user2
            user2.newSubscriber user1
            tempVar <- {Status="Success"; Data= username1 + " subscribed " + username2}
        else
            tempVar <- {Status="Error"; Data="Wrong details!!"}
        tempVar
    
    member this.retweet username password text =
        let temp = (this.SendTweet username password text true).Data
        let tempVar:ServerReply = {Status="Success"; Data=temp}
        tempVar
    
    member this.querySubscribedTweets username password =
        let mutable tempVar:ServerReply = {Status=""; Data=""}
        if this.CheckDetails username password then
            let user = this.userDetails username
            let tempStr = user.Subscribed() |> List.map(fun x-> x.MyTweets()) |> List.concat |> List.map(fun x->x.ToString()) |> String.concat "\n"
            tempVar <- {Status="Success"; Data= "\n" + tempStr}
        else
            tempVar <- {Status="Error"; Data="Wrong details!!"}
        tempVar  
    
    member this.queryHashTag hashtag =
        let mutable tempVar:ServerReply = {Status=""; Data=""}
        if hashtags.ContainsKey(hashtag) then
            let tempStr = hashtags.[hashtag] |>  List.map(fun x->x.ToString()) |> String.concat "\n"
            tempVar <- {Status="Success"; Data= "\n" + tempStr}
        else
            tempVar <- {Status="Error"; Data="Data not found!!"}
        tempVar
    
    member this.queryMention mention =
        let mutable tempVar:ServerReply = {Status=""; Data=""}
        if mentions.ContainsKey(mention) then
            let tempStr = mentions.[mention] |>  List.map(fun x->x.ToString()) |> String.concat "\n"
            tempVar <- {Status="Success"; Data= "\n" + tempStr}
        else
            tempVar <- {Status="Error"; Data="Data not found!!"}            
        tempVar
    
    member this.Logout username password =
        let mutable tempVar:ServerReply = {Status=""; Data=""}
        if this.CheckDetails username password then
            let user = this.userDetails username
            tempVar <- {Status="Success"; Data="User is logged out!!"}
            user.Logout()
        else
            tempVar <- {Status="Error"; Data="Wrong details!!"}              
        tempVar
    
    override this.ToString() = tweets.ToString() + "\n" + users.ToString() + "\n" + hashtags.ToString() + "\n" + mentions.ToString()
          
let twitter =  new TwitterEngine()

let registerActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdRegister(username,password,webSocket) ->
            if username = "" then
                return! loop()
            mailbox.Sender() <? twitter.RegisterUser username password webSocket|> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorRegister = spawn system "register" registerActor

let loginActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdLogin(username,password,webSocket) ->
            if username = "" then
                return! loop()
            mailbox.Sender() <? twitter.Login username password webSocket|> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorLogin = spawn system "login" loginActor

let sendTweetActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdSend(username,password,tweetData,false) -> 
            mailbox.Sender() <? twitter.SendTweet username password tweetData false |> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorSend = spawn system "send" sendTweetActor

let subscribeActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdSubscribe(username,password,subsribeUsername) -> 
            mailbox.Sender() <? twitter.subscribe username password subsribeUsername |> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorSubscribe = spawn system "subscribe" subscribeActor

let retweetActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdRetweet(username,password,tweetData) -> 
            mailbox.Sender() <? twitter.retweet  username password tweetData |> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorRetweet = spawn system "retweet" retweetActor

let querySubsTweetsActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdQueryST(username,password ) -> 
            mailbox.Sender() <? twitter.querySubscribedTweets  username password |> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorQuerySubsTweets = spawn system "querysubstweets" querySubsTweetsActor 

let queryHashtagActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdQueryHashtag(queryhashtag) -> 
            mailbox.Sender() <? twitter.queryHashTag  queryhashtag |> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorQueryHashtag = spawn system "queryhashtag" queryHashtagActor

let queryMentionActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdQueryMention(at) -> 
            mailbox.Sender() <? twitter.queryMention  at |> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorQueryMention = spawn system "querymention" queryMentionActor

let logoutActor (mailbox: Actor<_>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        match message  with
        |   CmdLogout(username,password) ->
            mailbox.Sender() <? twitter.Logout username password |> ignore
        | _ ->  failwith "Invalid Operation "
        return! loop()     
    }
    loop ()
let actorLogout = spawn system "logout" logoutActor

type CommandFormat = {
    Option : string
    Username : string
    Password : string
    UsernameSubs : string
    QueryST : string
    QueryHashtag : string
    QueryMention : string
}

type ActAPI =
    | CmdAPI of CommandFormat * WebSocket

let twitterAPIActor (mailbox: Actor<ActAPI>) = 
    let rec loop () = actor {        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        
        match message  with
        |   CmdAPI(message,webSocket) ->
            let mutable option = message.Option
            let mutable username=message.Username
            let mutable password=message.Password
            let mutable usernameSubs=message.UsernameSubs
            let mutable queryST=message.QueryST
            let mutable queryHashtag=message.QueryHashtag
            let mutable queryMention=message.QueryMention
            let mutable action = actorRegister <? CmdRegister("","",webSocket)
            match option with
            | "register" ->
                action <- actorRegister <? CmdRegister(username,password,webSocket)
            | "login" ->
                action <- actorLogin <? CmdLogin(username,password,webSocket)
            | "send" ->
                action <- actorSend <? CmdSend(username,password,queryST,false)
            | "subscribe" ->
                action <- actorSubscribe <? CmdSubscribe(username,password,usernameSubs )
            | "retweet" ->
                action <- actorRetweet <? CmdRetweet(username,password,(twitter.AllTweets().[queryST].tweetText))
            | "queryST" ->
                action <- actorQuerySubsTweets <? CmdQueryST(username,password ) 
            | "queryH" ->
                action <- actorQueryHashtag <? CmdQueryHashtag(queryHashtag )           
            | "queryM" ->
                action <- actorQueryMention <? CmdQueryMention(queryMention)
            | "logout" ->
                action <- actorLogout <? CmdLogout(username,password)
            let response: ServerReply = Async.RunSynchronously (action, 1000)
            sender <? response |> ignore
            return! loop()     
    }
    loop ()
let apiActor = spawn system "APIActor" twitterAPIActor

apiActor <? "" |> ignore

printfn "Twitter Server is running!!" 

///
/// 
/// WEB SOCKET IMPLEMENTATION
/// 
/// 

let twitterWS (webSocket : WebSocket) (context: HttpContext) =
    socket {
    let mutable loop = true

    while loop do
      let! message = webSocket.read()
      
      match message with
      
      | (Text, data, true) ->
        let str = UTF8.toString data

        let mutable json = Json.deserialize<CommandFormat> str
        printfn "%s" json.Option

        let mutable option= json.Option
        let mutable username=json.Username
        let mutable password=json.Password
        let mutable queryST=json.QueryST
        
        if option = "send" then
            let user = twitter.UserDetails().[username]
            let isRetweet = false
            let tweet = Tweet(DateTime.Now.ToFileTimeUtc() |> string, queryST, isRetweet)
            for dummyUser in user.Subscribers() do
                if dummyUser.Login() then
                    let reply =
                        (string("Tweet from subscribed user! "+tweet.tweetText))
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment                            
                    do! dummyUser.MySocket().send Text reply true 

        if option = "retweet" then
            let user = twitter.UserDetails().[username]
            let isRetweet = true
            let tweet = Tweet(DateTime.Now.ToFileTimeUtc() |> string, twitter.AllTweets().[queryST].tweetText, isRetweet)
            for dummyUser in user.Subscribers() do
                if dummyUser.Login() then
                    let reply =
                        (string("Retweeted message! "+tweet.tweetText))
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment                            
                    do! dummyUser.MySocket().send Text reply true                 

        let mutable action = apiActor <? CmdAPI(json,webSocket)
        let response: ServerReply = Async.RunSynchronously (action, 10000)

        let reply =
          Json.serialize response
          |> System.Text.Encoding.ASCII.GetBytes
          |> ByteSegment
        do! webSocket.send Text reply true

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true

        loop <- false

      | _ -> ()
    }

type typeTweet = {
    Username: string
    Password: string
    Data : string
}

let getString (rawForm: byte[]) =
    System.Text.Encoding.UTF8.GetString(rawForm)

let fromJson<'a> json =
    JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a

let funcTweet (tweet: typeTweet) = 
    let serverJson: CommandFormat = {Option = "send"; Username = tweet.Username; Password = tweet.Password; UsernameSubs = ""; QueryST = tweet.Data; QueryHashtag = ""; QueryMention = ""} 
    let action = apiActor <? CmdAPI(serverJson,Unchecked.defaultof<WebSocket>)
    let reply = Async.RunSynchronously (action, 1000)
    reply

let funcQuerySubTweets (username,password) = request (fun r ->
  let serverJson: CommandFormat = {Option = "queryST"; Username = username; Password = password; UsernameSubs = ""; QueryST = ""; QueryHashtag = ""; QueryMention = ""} 
  let action = apiActor <? CmdAPI(serverJson,Unchecked.defaultof<WebSocket>)
  let reply: ServerReply = Async.RunSynchronously (action, 1000)
  if reply.Status = "Success" then
    OK (sprintf "Tweets: %s" reply.Data) 
  else
    NOT_FOUND (sprintf "Error: %s" reply.Data))

let funcQueryHashtags hashtag = request (fun r ->
  let serverJson: CommandFormat = {Option = "queryH"; Username = ""; Password = ""; UsernameSubs = ""; QueryST = ""; QueryHashtag = "#"+hashtag; QueryMention = ""} 
  let action = apiActor <? CmdAPI(serverJson,Unchecked.defaultof<WebSocket>)
  let reply = Async.RunSynchronously (action, 1000)
  if reply.Status = "Success" then
    OK (sprintf "Tweets: %s" reply.Data) 
  else
    NOT_FOUND (sprintf "Error: %s" reply.Data))

let funcQueryMentions mention = request (fun r ->
  let serverJson: CommandFormat = {Option = "queryM"; Username = ""; Password = ""; UsernameSubs = ""; QueryST = ""; QueryHashtag = ""; QueryMention = "@"+mention} 
  let action = apiActor <? CmdAPI(serverJson,Unchecked.defaultof<WebSocket>)
  let reply = Async.RunSynchronously (action, 1000)
  if reply.Status = "Success" then
    OK (sprintf "Tweets: %s" reply.Data) 
  else
    NOT_FOUND (sprintf "Error: %s" reply.Data))

let Test  = request (fun r ->
    let reply = r.rawForm
                |> getString
                |> fromJson<typeTweet>
                |> funcTweet
    if reply.Status = "Error" then
        reply
            |> JsonConvert.SerializeObject
            |> UNAUTHORIZED
    else
        reply
            |> JsonConvert.SerializeObject
            |> CREATED) >=> setMimeType "application/json"

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake twitterWS
    path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") twitterWS
    GET >=> choose [
         pathScan "/query/%s/%s" funcQuerySubTweets
         pathScan "/queryhashtags/%s" funcQueryHashtags 
         pathScan "/querymentions/%s" funcQueryMentions  
         ]
    POST >=> choose [
         path "/sendtweet" >=> Test  
         ]
    NOT_FOUND "Found no handlers." ]

[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
  0
