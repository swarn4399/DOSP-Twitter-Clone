# TwitterDOSP
 A clone of the Twitter app with a Twitter Engine and a Client Simultor, implemented in F# using WebSockets.

# Project 4 part II

# Team: Swarnabha Roy

In this project, IimplementedaTwitter-like systemin F#usingtheWebSocketSharperand
SuavewebframeworktoimplementaWebSocketinterface.IhavedesignedaJSONbasedAPI
that represents all messages and their replies, along with errors.

Suave was used in the Server side code, and WebSocketSharper was used in the Client side code
to create web sockets and ensure communication between the twitter engine and client
simulator..

TheTwitter Enginehasthefollowingfunctionalities,eachofwhichishandledbyadifferent
actor:

**Registeraccount** - takesastringasanargumentforusernameandaddsanewusertoamap.
Duplicate entries are not allowed ie. no two users can have the same username

**Sendtweet** - takestwostringsasargumentsfortheusernameoftheusersendingthetweetand
the tweet content. Only existing users can send tweets.

**Subscribetouser’stweets** - takestwostringsasargumentsfortheusernameoftheuserwanting
tosubscribe,andtheusernameoftheuserhe/shewantstosubscribe.Bydefault,allusersare
subscribedtotheirownaccounts,ie.ifauserhasnotsubscribedtoanyotheruser,he/shewillbe
able to view only his/her own tweets.

**Re-tweets** - similartoSendtweet.Ifauserwantstoshareatweetwithallofhis/hersubscribers,
he/she can just retweet it.

**Querytweetssubscribedto** - takesastringasanargumentfortheusernameoftheuser.This
displaysallthetweetsauserhassubscribedto,ie.his/herowntweetsplustweetsfrompeople
he/she has subscribed to.

**Querytweetswithspecifichashtags** - takesastringasanargumentforthehashtagwhicha
user wants to query. This displays all the tweets containing the specified hashtag.

**Querytweetsinwhichtheuserismentioned** - takesastringasanargumentfortheusername
oftheuserwhowantstoseehis/hermentions.Thisdisplaysallthetweetsinwhichthespecified
user has been mentioned.

**connect/ disconnect** - takes a string for the username of the userwanting to connect or
disconnect.Thisissimilartogoingonline/offline.Whenauserisconnected,ifhe/shedoesany
activityie.tweet/retweet,allofhis/hertweetsandretweetsaresentasalivefeedtoallofhis/her
subscribers.Thatis,thetweets/retweetsareavailablewithoutquerying.Thisstopsandreturnsto
the previous scenario as soon as the user disconnect (goes offline).


**How to run:**

**Server:**

dotnet run

**Client:**

dotnet fsi TwitterClient.fsx

**Command options:**
Register:register,<username>,<password>
Login:login,<username>,<password>
Subscribe:subscribe,<username>
Send Tweet:send,<tweet content>
Retweet:retweet,<tweet ID>
Query the subscribed tweets:queryST
Query Hashtags:queryHashtag,<#hashtag>
Query Mentions:queryMention,<@mention>
