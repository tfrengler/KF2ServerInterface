# KF2ServerInterface
Go-project to handle KF2 server web admin interface via command line

Just a small utility I wrote for myself. It's primary purpose is to monitor my Killing Floor 2 servers and switch maps if the server is empty and not on the right map.

TODO:
- Incorporate UpdateServerSetup.csx into the go-code, and have it activateable via switches
- To faciliate the above: incorporate the extra config-options from the csx into the go-variant's config.json
- Add an option to shut down the server, after a certain hour (when server is empty) (can be done with a POST to /console with {command: exit})