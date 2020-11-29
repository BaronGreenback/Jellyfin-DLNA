Provides IP4/IP6 DLNA functionality for Jellyfin.

Used by:

https://github.com/BaronGreenback/Jellyfin-DLNA-Server

https://github.com/BaronGreenback/Jellyfin-DLNA-PlayTo


**Settings**

**EnableSsdpTracing**

Enables ssdp low level tracing. Filtering can be done via **SsdpTracingFilter**

**UdpSendCount**

Changes the number of times each ssdp message is transmitted.
        
**UdpPortRange** 

Defines the port range for global UDP connections. Default 49152-65535

**DlnaVersion**

Specifies the dlna version to use. DLNA Version 2 is not yet fully implemented. 

**EnableMultiSocketBinding**

Enables multi socket bindings.

**PermittedDevices**

Specifies a list of IP devices that are permitted (or denied) from connecting via SSDP. 
This overrides the network.xml LAN settings.


****Core Changes****

Profiles can now be assigned an IP address, enabling two different devices reporting the same dlna sub-system to be correctly identified.

**AutoCreatePlayToProfiles**

When true, disk profiles are be created for devices that are unknown, enabling easy editing.
