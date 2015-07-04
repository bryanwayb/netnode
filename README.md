Notice
==
This project has been superseded by [NodeSocket for C#](https://github.com/bryanwayb/nodesocket-csharp), as a simpler take on the same concept. This has been left here for documentation purposes only and is no longer maintained.

Other links that may be useful:
* [NodeSocket for NodeJS](https://github.com/bryanwayb/nodesocket-nodejs)
* [NodeSocket for web browsers (using websockets)](https://github.com/bryanwayb/nodesocket-browserify)
*  [NodeSocket for PHP](https://github.com/bryanwayb/nodesocket-php)

# NetNode

NetNode aims to be an extensible cross language library for implementing network communication as self contained nodes.

Note: This project is currently in **Alpha** and is under active development.
All pull requests are welcomed and will be reviewed in a timely fashion.

*NetNode is currently only implemented in C#*

##Server

```c#
// Sets the IP address and port as 127.0.0.1:8000
NetNode.NodeIP ipConfig = new NetNode.NodeIP(new byte[] { 127, 0, 0, 1 }, 8000);

// Add IP and port configured above to a bindable connection pool (IPs and Ports that are to be listened to)
NetNode.Node.Default.AddNodeIP(ipConfig, NetNode.NodeIPType.Bindable);

NetNode.Node.Default.ServerStart(); // Starts the server
```
While the above example will work and function as intended, it will do absolute nothing other than listen for connections from a NetNode client. See the section below on [Configuring Server Listeners](#configuring-server-listeners) to see how to setup server functions.

##Client

```c#
// Same as the server, sets the IP address and port as 127.0.0.1:8000
NetNode.NodeIP ipConfig = new NetNode.NodeIP(new byte[] { 127, 0, 0, 1 }, 8000);

// Add the IP and port to a connectable pool
NetNode.Node.Default.AddNodeIP(ipConfig, NetNode.NodeIPType.Connectable);

NetNode.Node.Default.ClientStart(); // Starts the client
```
Using the above code, we're able to make a connection to a NetNode server. Starting the client will begin opening connections with the configured IP and ports.

Once a server has been verified, a client ping thread is used to ensure that the connection is kept alive over network interfaces that are prone to disconnect stale connections. (Default ping interval is 3 seconds but is configurable)

##Configuring Server Listeners

Listeners are used to denote functions to be performed by the server. They're identified by either a string or byte array and their callbacks accept a byte array as their only parameter and return a byte array.
```c#
NetNode.Node.Default.AddListener("ExampleRemoteFunction", delegate(byte[] param)
{
	Console.WriteLine("This is an example function that was called from the client.");
	return null;
});
```
It's important to know that Listener callbacks will never pass a null value in place of the byte array, **even if the client sends a null value**. If a null value is sent, the parameter will instead be an array with a length of 0.

The same is true for when the client receives the returned result from the Listener callback. See below on [Executing Remote Functions](#executing-remote-functions).

##Executing Remote Functions

Sending functions to a NetNode server is as easy as setting up a basic Listener on the server itself. *It's good to note that this is a synchronous function.*
```c#
byte[] returnedBytes = NetNode.Node.Default.ClientExecuteFunction(new NetNode.NodePortIPLink(new byte[] { 127, 0, 0, 1 }, 8080), "ExampleRemoteFunction", null);
```
Calling the above will send a request to the server NetNode binded on IP and port 127.0.0.1:8000 to execute a function identified as "ExampleRemoteFunction".

Above in the [section where "ExampleRemoteFunction" was defined](#configuring-server-listeners), we have the returning value being null. However, returnedBytes won't be null, but instead a byte array, with a length of 0. This was a design choice.

Note: The client must have a connection to the IP and port that is intended to receive the remote function request, meaning that unconnected requests will not queue while waiting for a connection. To avoid complications, ensure this method only gets called after the [OnStart() client callback is called](#client-callbacks).

##Server and Client Callbacks

Once a server or client NetNode instance is started, there needs to be a way to get updates on the current operation status. That's where callbacks come in.

####Server Callbacks

```c#
NetNode.Node.Default.SetServerCallbacks(new ServerCallbacks()
{
	OnStartError = delegate(ServerStatus status)
	{
		Console.WriteLine("Server start failed. Client status: " + status.ToString());
	},
	OnStart = delegate()
	{
		Console.WriteLine("Server started");
	},
	OnStop = delegate()
	{
		Console.WriteLine("Server stopped");
	},
	OnSocketConnect = delegate(SocketPoolEntry entry, NetNode.NodePortIPLink link)
	{
		Console.WriteLine("Server connected to " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
	},
	OnSocketDisconnect = delegate(SocketPoolEntry entry, NetNode.NodePortIPLink link)
	{
		Console.WriteLine("Server disconnected from " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
	},
	OnError = delegate()
	{
		Console.WriteLine("Server encountered an error");
	},
	OnSocketError = delegate(SocketPoolEntry entry, NetNode.NodePortIPLink link, SocketError error)
	{
		Console.WriteLine("Server socket error on " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port + ": " + error.ToString());
	},
	OnSocketBind = delegate(SocketPoolEntry entry, NetNode.NodePortIPLink link)
	{
		Console.WriteLine("Server bound to " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
	},
	OnSocketUnbind = delegate(SocketPoolEntry entry, NetNode.NodePortIPLink link)
	{
		Console.WriteLine("Server unbound from " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
	}
});
```

####Client Callbacks

```c#
NetNode.Node.Default.SetClientCallbacks(new ClientCallbacks()
{
	OnStartError = delegate(ClientStatus status)
	{
		Console.WriteLine("Client start failed. Client status: " + status.ToString());
	},
	OnStart = delegate()
	{
		Console.WriteLine("Client started");
	},
	OnStop = delegate()
	{
		Console.WriteLine("Client stopped");
	},
	OnSocketConnect = delegate(SocketPoolEntry entry, NetNode.NodePortIPLink link)
	{
		Console.WriteLine("Client connected to " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
	},
	OnSocketDisconnect = delegate(SocketPoolEntry entry, NetNode.NodePortIPLink link)
	{
		Console.WriteLine("Client disconnected from " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
	},
	OnError = delegate()
	{
		Console.WriteLine("Client encountered an error");
	},
	OnSocketError = delegate(SocketPoolEntry entry, NetNode.NodePortIPLink link, SocketError error)
	{
		Console.WriteLine("Client socket error on " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port + ": " + error.ToString());
	}
});
```

##Filters

Filters were introduced as a method of setting global options on a IP address and port, regardless of the Node pool it belongs to or the intentions for it.

They have no specific functionality and their usage depends on the operations being performed.

Here's an example filter to set the maximum number of connections that a bound IP and port are able to have open at a single instant.
```c#
// Only allow for 1 connection on local IP 127.0.0.1 and port 8000
NetNode.Filters.AddFilter(new NetNode.NodePortIPLink(new byte[] { 127, 0, 0, 1 }, 8000), new NetNode.Filter.MaxPendingQueue(1));
```

# TODO
* Enable "client as server" functionality, where a client registers as a "server" to the server node
* Extend development to other languages. PHP, JavaScript(NodeJS/WebSockets), and C/C++ are languages I'd like to implement NetNode in