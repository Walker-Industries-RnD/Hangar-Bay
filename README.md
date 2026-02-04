<img src="docs/assets/hangarbay.png" alt="Hangar Bay" width="100%"/>

**The first secure, Stride3D focused modding system designed to package, validate, and load scripts, assets, and behaviors without trusting user input.**

HangarBay is a mod development system for Stride3D, created for the XRUIOS but easily usable by other programs. Atop this, the framework of HangarBay makes the system nearly engine agnostic.

HangarBay uses Pariah Cybersecurity, promising PQC Cryptographic Resistance.

---


## How it works

HangarBay separates **mod creation**, **mod validation**, and **mod execution** into clearly defined stages, ensuring unsafe or malformed content never reaches the engine runtime.

#### At a high level:

- First, developers create a **Mod Type**, which is signed publicly and has a ruleset regarding what asset types are/aren't allowed and what DLLs are/aren't allowed. 

- Developers then create the ModType, which allows them to create **Mods** following the rules set by the application developer. They can validate the ModType through it's Public Signature.

- Finally, users install the mods onto their systems, with the application handling the mods as the developer wishes. Users can enable/disable and uninstall mods.

#### At the modding level

- Scripts are compiled and loaded as DLLs

- Assets are loaded dynamically at runtime through content manager

- Because of the way HangarBay works, you can save items as a prefab which can then be loaded into the scene (Scripts and all)

---
<br>
<p align="center">
  <a href="https://walkerindustries.xyz">Walker Industries</a> •
  <a href="https://discord.gg/H8h8scsxtH">Discord</a> •
  <a href="https://www.patreon.com/walkerdev">Patreon</a>
</p>

<p align="center">
  <a href="https://walker-industries-rnd.github.io/Hangar-Bay/1-start-here/1-welcome.html" 
     style="font-size: 1.4em; color: #58a6ff; text-decoration: none;">
    <strong> Documentation • Examples • Design </strong>
  </a>
</p>



<div align="center">

| ![WalkerDev](docs/assets/walkerdev.png)                                                                                  | ![Kennaness](docs/assets/kennaness.png)                                                                                                                                     |
| ------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Code by WalkerDev**<br>“Loving coding is the same as hating yourself”<br>[Discord](https://discord.gg/H8h8scsxtH) | **Art by Kennaness**<br>“When will I get my isekai?”<br>[Bluesky](https://bsky.app/profile/kennaness.bsky.social) • [ArtStation](https://www.artstation.com/kennaness) |

</div>

<br>

## Dependencies


- Pariah Cybersecurity (And it's dependencies)
- Stride3D (Or Stride.CommunityTooklit for minimalists)
- DouglasDwyer.CasCore
---

## Runtime Loading

HangarBay does not rely on editor-time compilation.

At runtime:

1. Script DLLs are loaded
    
2. Prefabs and assets are deserialized
    
3. Components bind automatically
    
4. Execution hooks are called
    

If a mod contains a spinning cube prefab, the cube spins. No special handling required.

---


## Security

*HangarBay includes several layers of protection to keep mods safe and isolated like:*

- CAS for Simple Yet Effective Policy Setting
- 
- User-specific HMAC-SHA256 signatures verify mod activation files (.enabled + .sig) — only the user who enabled the mod can make it work.

- AES-256 encryption protects all stored secrets (including post-quantum signing keys) inside an encrypted secret bank.

- Each mod runs with its own isolated ContentManager and virtual filesystem root — no asset overwriting or path conflicts between mods.

- Strict mod type rules enforce allowed / required / forbidden file extensions and DLL names before a mod is loaded.

- Mod DLLs are checked for:
    - Strong-name / public key match against trusted versions
    - Exact SHA256 hash comparison

- Outdated or incompatible mod DLLs can be replaced with newer system versions.

- Mod type definitions are cryptographically signed using post-quantum algorithms (resistant to future quantum attacks).

- Mod metadata (images, thumbnails, etc.) includes xxHash values to detect tampering or unauthorized replacement.

- Secrets are tied to a user passphrase + device fingerprint — keys cannot be easily stolen or moved to another machine.

- No global asset merging — every mod’s content stays fully separated from the engine and other mods.


## Developer Experience

HangarBay is engine-agnostic by design, focused on Stride3D.

However, it is not impossible to make small tweaks and use it on Unity or Godot. In fact, it's likely possible to use with a far greater array of engines thanks to P/Invoke.


Mod developers:

- Do not touch engine internals
    
- Do not write custom loaders
    
- Do not need editor plugins
    

They simply:

1. Choose a Mod Type
    
2. Add assets and scripts
    
3. Build the mod
    
4. Drop it into `Mods/`
    

---


## License & Artwork

**Code:** [NON-AI MPL 2.0](https://raw.githubusercontent.com/non-ai-licenses/non-ai-licenses/main/NON-AI-MPL-2.0)  
**Artwork:** — **NO AI training. NO reproduction. NO exceptions.**

<img src="https://github.com/Walker-Industries-RnD/Malicious-Affiliation-Ban/blob/main/WIBan.png?raw=true" align="center" style="margin-left: 20px; margin-bottom: 20px;"/>

> Unauthorized use of the artwork — including but not limited to copying, distribution, modification, or inclusion in any machine-learning training dataset — is strictly prohibited and will be prosecuted to the fullest extent of the law.
