---
name: Sync Issue Template
about: Template for Sync Issue
title: ""
labels: ''
assignees: ''

---
## Description

1. Sync the values in the **Synced** list below.
2. Create an integration test for all values (1 for server changing the value and 1 for client changing the value if applicable)
3. Create a command to test changing the value in game [see similar command](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/blob/development/source/GameInterface/Services/Towns/Commands/TownDebugCommand.cs#L455)

Also see additional information at the bottom for more information and templates

<!-- Describe why this issue is needed. -->
**Key**
| Sync Type | Description |
|:-----------------|:---------|
| Server side only | only allow the running of the function on the server side |
| Client side only | only allow the running of the function on the client side |
| Client side sync | request the server to change the value, server then changes the value and replies to the client allowing the change |
| Server side sync | only server allows running of the function and send to all clients the value changed and clients update the value/call original function |

**Synced**
<!-- Add all method/fields below -->
| Method/Field Name | Sync Type |
|:-----------------:|:---------:|
| TODO              | TODO |

**Deferred**
<!-- Add deffered (covered by a field in the Synced table) --->
None

**Externally Deferred**
<!-- Add externally deffered (covered by a different sync) --->
None

**Non-Synced**
None

## Intended Design
<!-- Provide any relevant design documents, create any if complexity requires. -->
**Message networking**
![image](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/assets/15619189/48f5f821-8e8f-46cd-8d73-4c2547755aea)
**Integration Testing**
![image](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/assets/15619189/0e57923e-3af7-403a-aac3-aece50d7acb1)
## Location
<!-- Add where changes for this issue will exist. -->
<!-- Add any related files here. -->
Create a branch based from [development](https://github.com/Bannerlord-Coop-Team/BannerlordCoop)

## Related Issues
<!-- Add any related issues here (Child of #EPIC/ Blocked by #SOMETHING). -->
N/A

## Requirements
<!-- Add testable requirements if needed. -->
N/A

## Additional information
<!-- Add all information that might be useful while working on this issue here. (e.g. places in the code to look at) -->
Using the [GameInterface Service Tempale](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/tree/development/source/GameInterface/Services/Template) create commands to test the synced values.

For network communication use [Server Service Template](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/tree/development/source/Coop.Core/Server/Services/Template) and [Client Service Template](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/tree/development/source/Coop.Core/Client/Services/Template)

It is recommended to start with an integration test, you can create one using the [test template](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/tree/development/source/Coop.IntegrationTests/Template).

## Definition of Done
- [ ] Class level comments exist for all new classes.
- [ ] XUnit tests exist for every method that does not require the game to be ran.
- [ ] Commands exist for all new sync items.
<!-- Create more required items as needed. -->
