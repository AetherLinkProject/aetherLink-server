# AetherlinkServer

## Getting Started

Before running Aetherlink Server, you need to prepare the following infrastructure components, as they are essential for the project's operation:
* Kvrocks

The following command will clone Aetherlink Server into a folder. Please open a terminal and enter the following command:
```Bash
git clone https://github.com/AetherLinkProject/aetherLink-server.git
```

The next step is to build the project to ensure everything is working correctly. Once everything is built and configuration file is configured correctly, you can run as follows:

```Bash
# enter the aetherlink-server folder
cd aetherlink-server

# publish
dotnet publish src/Aetherlink.Worker/Aetherlink.Worker.csproj -o aetherlink-server/Worker

# enter aetherlink folder
cd Worker

# ensure that the configuration file is configured correctly
# run Aetherlink Worker
dotnet AetherlinkWorker/Aetherlink.Worker.dll
```

After starting all the above services, Aetherlink Server is ready to provide external services.

## Modules

Aetherlink Server includes the following services:

- `Aetherlink.Worker`: Aetherlink server reporting aetherlink node.
- `Aetherlink.Multisignature`: Aetherlink multi signature.

## Contributing

We welcome contributions to the Aetherlink Server project. If you would like to contribute, please fork the repository and submit a pull request with your changes. Before submitting a pull request, please ensure that your code is well-tested.