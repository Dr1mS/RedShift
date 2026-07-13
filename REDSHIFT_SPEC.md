# REDSHIFT — Spécification complète v1.0

> Codename : **REDSHIFT** (fuite devant la fin de l'univers — décalage vers le rouge). Renommable sans impact.
> Document de référence unique. Toute décision non couverte ici remonte au propriétaire du projet (Adrien) avant implémentation.
> Compagnon opérationnel : `CLAUDE.md` (conventions, workflow Unity-MCP, definition of done).

---

## 1. Identité du projet

**Pitch** — L'univers meurt, système par système. Un équipage de 1 à 4 pilleurs saute de système stellaire en système stellaire à bord d'un vaisseau-mère, pille planètes et ruines d'une civilisation disparue pour atteindre un quota de ressources avant que l'étoile locale n'explose en supernova, et améliore son Moteur d'Éversion jusqu'à pouvoir quitter l'univers lui-même.

**Genre** — Co-op extraction / horreur légère / exploration, vue FPS. « Lethal Company × Outer Wilds ».

**Piliers de design (dans l'ordre de priorité)** :
1. **Le sablier stellaire** — chaque système est un timer diégétique visible : l'étoile gonfle, rougit, gronde. La tension monte mécaniquement ET visuellement.
2. **Stream-friendly par construction** — voice proximité, situations à quiproquos, morts spectaculaires, escape à 10 secondes près, moments « clip ».
3. **Petites planètes, grandes décisions** — planètes sphériques arpentables en 3-5 min, choix constant risque/temps/récompense.
4. **La coopération paie** — rôles émergents (pilote / mineur / rat de ruines / vigie), jamais imposés.

**Références** — Lethal Company (boucle quota, ton, économie du loot), Outer Wilds (planètes sphériques, gravité, pilotage), FTL (carte stellaire à choix, tension de la fuite), Deep Rock Galactic (minage co-op, extraction timée).

**Contraintes assumées (issues du brief)** — Multijoueur = coût élevé mais non négociable (cœur du genre). Visuels complexes = évités (DA low-poly, §7). Génération procédurale = remplacée par du handcrafted randomisé (§4.9, §10). Durée de dev = contenue par un scope MVP verrouillé (§8, §9).

---

## 2. Décisions verrouillées

| # | Sujet | Décision | Justification |
|---|-------|----------|---------------|
| D1 | Moteur | Unity 6 LTS (6000.x, dernière LTS au setup), URP | Cible du tooling Unity-MCP, pipeline léger adapté low-poly |
| D2 | Netcode | **FishNet 4.x** + transport **FishySteamworks** (Facepunch.Steamworks) | Gratuit, performant, host-based P2P via Steam relay, pas de coût serveur. Alternative évaluable en P0 : PurrNet. Host = un joueur, 1-4 joueurs |
| D3 | Échelle monde | Un système = une scène. Rayon max du système : **8 km** depuis l'origine. Planètes : rayon **150-400 m**. **Pas de floating origin** | À 8 km, la précision float est ~1 mm : suffisant. Supprime le plus gros risque technique spatial |
| D4 | Gravité | Gravité sphérique custom (`GravitySource`/`GravityReceiver`), planètes **statiques** (pas d'orbites) | Les orbites n'apportent rien à la boucle et multiplient les bugs réseau |
| D5 | Vaisseaux | Navettes 2 places : joueurs **assis** pendant le vol (pas de déplacement intérieur). Vaisseau-mère praticable **uniquement posé ou en FTL** (référentiel immobile) | Élimine entièrement le problème « marcher dans un vaisseau en mouvement en multi », le plus dur du genre. Réévaluable post-1.0 |
| D6 | Intérieurs de ruines | **Zones-poches** : intérieurs placés à plat, loin dans la scène (y-up standard), reliés par portes-portails avec sas de transition | Technique Lethal Company. Rend NavMesh + IA monstres triviaux (le NavMesh ne fonctionne pas sur sphère) |
| D7 | Procédural | **Interdit** pour le terrain. Planètes = templates handcrafted à variantes. Ruines MVP = layouts fixes ; 1.0 = assemblage seedé de rooms préfabriquées. Randomisé : loot, gisements, monstres, événements | Rejouabilité sans le coût du procgen |
| D8 | Multijoueur-first | FishNet installé en P0 ; tout système gameplay écrit `NetworkBehaviour`-aware dès sa création. Validation multi réelle en P4 | Retrofitter du netcode coûte 3× plus cher que le prévoir |
| D9 | Voice | **Vivox** (Unity Gaming Services, gratuit sous seuil MAU), audio positionnel 3D. Fallback : Dissonance | Voice proximité = pilier stream-friendly |
| D10 | Structure de partie | Partie = « Expédition » : 6-8 systèmes, ~4-6 h, sauvegardable entre systèmes (JSON côté host) | Run-based, sessions découpables |
| D11 | Distribution | Steam uniquement, Early Access, **9,99 €**, EN+FR au lancement | Positionnement Lethal Company / R.E.P.O. |
| D12 | Échec | Quota raté = perte d'un **Cœur d'intégrité** (3 au départ). 0 cœur = fin d'expédition. Mort dans la supernova = perte de l'inventaire porté | Tension sans wipe punitif |

---

## 3. Boucle de gameplay

### 3.1 Macro (une expédition)
`Nouveau système → collecte sous timer → saut FTL → shop/upgrades/choix de route → système suivant (quota et danger croissants) → ... → Drive d'Éversion niv. 6 → système final scripté → fin`

### 3.2 Timeline d'un système (25-35 min, configurable par `SystemDef`)

| Phase | Durée | État de l'étoile | Contenu |
|-------|-------|------------------|---------|
| **Arrivée** | 1 min | Stable | Sortie FTL, le vaisseau-mère se pose auto sur la planète d'ancrage (cinématique in-engine courte). Scan système : carte des 3-5 planètes avec tags (biome, richesse estimée, ruine O/N, danger) |
| **Exploration** | 15-22 min | Stable → Instable | Sorties navettes, minage, ruines, loot. Dépôts en soute (seul le loot **déposé** compte pour le quota) |
| **Critique** | 5-8 min | Critique : l'étoile gonfle visiblement, teinte rouge globale, alarmes, événements d'instabilité (séismes, éruptions, EMP) en crescendo | Décision : dernier run risqué ou repli |
| **Collapse** | 90 s | Effondrement | Compte à rebours de décollage. Fenêtre de saut |
| **Saut** | — | Supernova | Quota OK + équipage à bord → saut, supernova visible par la baie arrière (**money shot**). Quota raté → saut quand même, −1 Cœur. Joueur pas à bord → mort |

### 3.3 Micro-boucle joueur
Se poser en navette → repérer (scanner) → miner un filon / fouiller une ruine → gérer poids et mains (gros objets = 2 mains, LC-style) → survivre aux monstres → remonter → déposer en soute → répéter ou changer de planète.

### 3.4 Phase FTL (5-8 min, sans timer)
- **Vente** : l'excédent de loot au-delà du quota se convertit en Crédits.
- **Shop** : upgrades (§4.7), consommables, réparation des Cœurs (cher).
- **Décraft** : les Artefacts précurseurs se démontent en Composants requis par certaines upgrades.
- **Carte stellaire** : choix du prochain système parmi **3 routes** (FTL-like) : sûre/pauvre, moyenne, riche/dangereuse. Tags visibles avant choix.
- **Lore** : terminal de bord pour relire les fragments collectés.

---

## 4. Systèmes de jeu

### 4.1 Joueur
- FPS, controller custom **Rigidbody aligné sur la gravité locale** (marche, sprint avec stamina, saut, accroupi). Pas de KCC asset payant.
- Inventaire LC-style : 4 slots rapides + mains ; objets lourds portés à 2 mains (bloquent sprint/outils).
- Équipement de base : lampe frontale (batterie), scanner (ping des items/gisements/entrées de ruines dans un rayon), pioche laser T1.
- Ping contextuel (molette) visible par l'équipe. Stamina, oxygène **non** géré en MVP (combinaison autonome — un système d'O2 est listé post-1.0).

### 4.2 Gravité
- `GravitySource` : champ sphérique (rayon d'influence, g à la surface, priorité). `GravityReceiver` : appliqué aux joueurs, navettes, props physiques, loot.
- Transition entre champs par priorité + hystérésis (pas d'oscillation à la frontière).
- En espace ouvert (aucun champ) : micro-gravité, la navette est le seul moyen de déplacement sûr (jetpack = upgrade).

### 4.3 Navettes
- 2 places (pilote + passager), soute ~8 slots. 2 navettes au vaisseau-mère dès le départ.
- Vol 6DOF **assisté** (flight assist par défaut : amortissement + auto-level près du sol ; désactivable pour les pilotes confiants). Vitesse max ~100 m/s → trajets inter-planètes de 30-90 s.
- Atterrissage : auto-snap sous seuil de vitesse/inclinaison, sinon dégâts coque.
- Coque = HP ; destruction = explosion, occupants éjectés (ragdoll — clip potentiel), épave lootable pour récupérer la soute.
- Sièges : interactable → le joueur est parenté au siège, controller désactivé, caméra libre limitée. Ownership réseau de la navette transférée au pilote.

### 4.4 Vaisseau-mère (« l'Arche »)
- Praticable posé/FTL : soute principale (zone de dépôt quota), atelier (shop en FTL), cockpit (carte stellaire, scan), baie des navettes, terminal de lore, module du Drive d'Éversion (visuel qui évolue avec les niveaux).
- Le décollage/saut est déclenché au cockpit (n'importe quel joueur), avec vote si équipage incomplet à bord.

### 4.5 Minage
- Gisements en surface et dans des grottes peu profondes (handcraftées dans les templates de planètes). Filon = HP + table de rendement.
- Pioche laser : faisceau à chaleur (surchauffe = pause forcée). Le minage **fait du bruit et de la lumière** → attire la Nuée (§4.8). Minage AFK puni.
- Minerais = la base du quota. Tiers de minerai par difficulté de système.

### 4.6 Loot, quota, économie
- Tout item a `mass` et `value`. Le **quota est en valeur** (crédits-équivalents) de loot déposé dans la soute de l'Arche.
- Quota croissant par système : `quota(n) = base × growth^n` (valeurs de départ : base 400, growth 1.35 — à tuner).
- Deux ressources : **Quota** (consommé à chaque saut) et **Crédits** (excédent vendu, dépensé au shop).
- Artefacts précurseurs : forte valeur OU décraft en Composants d'upgrade — choix économique.

### 4.7 Progression / upgrades
- **Outils** : laser T2/T3, scanner longue portée, jetpack, sac dorsal (+2 slots), lampe T2, balise de repérage.
- **Navette** : soute +4, boost, bouclier thermique (survivre à la phase Critique dehors), +2 sièges.
- **Arche** : soute +, aimant de collecte (rayon autour de l'Arche posée), Cœur d'intégrité +1, fenêtre de décollage étendue.
- **Drive d'Éversion : 6 niveaux.** Chaque niveau ≥ 2 exige Crédits + **1 Fragment Précurseur** (uniquement dans les salles profondes des ruines, gardées). Lie ruines ↔ progression ↔ lore. Niveau 6 = accès au saut final.

### 4.8 Monstres

Principes : peu de monstres mais **lisibles et à contre-jeu clair** ; l'extérieur utilise des IA sans NavMesh (steering + alignement gravité), l'intérieur (poches) utilise NavMesh standard.

**MVP (3)** :
| Nom | Habitat | Comportement | Contre-jeu |
|-----|---------|--------------|-----------|
| **Fouisseur** | Ruines | Patrouille, mêlée, réagit au bruit | S'accroupir, contourner, lampe éteinte inutile (aveugle) |
| **Pâle** | Ruines | Stalker : suit les joueurs isolés, fuit la lumière directe, attaque de dos | Rester à deux, se retourner (LC-Bracken-like) |
| **Nuée** | Extérieur | Essaim attiré par le laser de minage et les lumières, harcèle | Miner par sessions courtes, leurres lumineux |

**1.0 (+4)** : Mimic de loot ; Léviathan des sables (désert, détecte les vibrations — marcher lentement) ; Sentinelle précurseure (garde les Fragments, puzzle/furtivité plutôt que DPS) ; Harceleur orbital (rare, attaque les navettes en vol — le pilote a peur aussi).
**R&D post-1.0** : l'**Écho** — enregistre et rejoue les voix des joueurs (Vivox buffer replay). Or de stream, complexité audio réelle : prototype gaté.

### 4.9 Planètes et ruines
- **Planètes** : templates handcraftés (sphère sculptée + props), déclinés en variantes par biome et par redressage (rotation/palette/props). Biomes 1.0 : Roche, Glace, Désert, Fongique. MVP : Roche + Glace.
- **Ruines** : entrée en surface → sas → **poche intérieure** (§D6). MVP : 2 layouts fixes par kit avec spawns randomisés. 1.0 : 3 kits (précurseur-temple, station minière effondrée, laboratoire), 12+ rooms modulaires par kit, assemblage par seed avec règles d'adjacence (graphe simple, **pas** de génération de géométrie).
- **Événements par système** (tirage 1-2) : tempête solaire (EMP navettes 30 s), pluie de météores localisée, planète-coffre (très riche + monstre alpha), marchand errant (mini-shop en cours de système), anomalie de gravité (zone inversée — chaos stream-friendly).

### 4.10 Supernova
- `StarDirector` : pilote le shader de l'étoile (échelle, granulation, teinte), la lumière globale du système, les couches d'alarmes audio, et le tirage des événements d'instabilité par phase.
- Collapse : onde de choc = sphère en expansion (vitesse ~réglée pour rattraper une navette non boostée) ; tout ce qu'elle touche meurt/explose. Le saut FTL de l'Arche se déclenche à T-0 ; séquence scriptée de fuite avec la supernova dans la baie arrière.

### 4.11 Mort et spectateur
- Mort = spectateur libre (caméra drone) jusqu'au saut ; commentaires possibles au voice (canal spectateur → morts entre eux + léger bleed vers les vivants proches du corps ? Non : canal morts séparé, simple). Respawn au système suivant.
- Corps lootable : ramener le corps à l'Arche évite la pénalité d'« assurance » (perte de crédits).

### 4.12 Lore et fin
- Les **Précurseurs** ont fui l'univers avant nous ; leurs ruines contiennent les plans du Drive (justification diégétique de l'upgrade finale). Fragments de lore scannables (terminaux, fresques, enregistrements) — collectés au terminal de l'Arche.
- **Fin** : Drive niv. 6 → système final scripté (étoile déjà en effondrement à l'arrivée, quota remplacé par un objectif unique : amorcer la Membrane). 2-3 variantes d'épilogue selon % de lore collecté. Durée cible du contenu final : 30-40 min.

---

## 5. Multijoueur

- **Topologie** : host-based (un joueur héberge), Steam P2P relay via FishySteamworks. Pas de serveurs dédiés. Solo = host local sans lobby.
- **Autorité** : état de jeu (phases, timer, quota, spawns, monstres, économie) = **server/host-authoritative**. Mouvement joueur et navette pilotée = **owner-authoritative** avec réconciliation légère (pas de compétitif, la triche n'est pas une menace prioritaire).
- **Sync sensibles** : parentage siège↔joueur, transfert d'objets tenus, portails de poche (téléport groupé joueur+objets), physique du loot (endormie par défaut, réveillée par interaction, autorité au host).
- **Voice** : Vivox positionnel 3D, portée ~20 m, canal radio d'équipe via item « talkie » (upgrade), canal morts séparé.
- **Sessions** : lobby Steam (invite amis + code), reconnexion en cours de système = spawn à l'Arche.

---

## 6. Architecture technique

### 6.1 Stack
Unity 6 LTS · URP · FishNet 4.x · FishySteamworks + Facepunch.Steamworks · Vivox · Input System · Cinemachine · ProBuilder (greybox) · UniTask (async) · NewtonSoft ou System.Text.Json (save).

### 6.2 Structure projet
```
Assets/
  _Project/
    Scripts/
      Core/          # utilitaires, event bus, machines à états (asmdef sans deps Unity si possible)
      Gameplay/      # joueur, gravité, minage, loot, monstres, navettes
      Networking/    # wrappers FishNet, lobby, sync helpers
      Meta/          # économie, quota, upgrades, save, carte stellaire
      UI/
    Data/            # ScriptableObjects (ItemDef, CreatureDef, PlanetDef, SystemDef, UpgradeDef, EventDef, LoreDef)
    Prefabs/  Scenes/  Art/  Audio/  Settings/
  Tests/
    EditMode/        # logique pure : quota, économie, tirages, timer
    PlayMode/        # gravité, vol, interactions, portails
```
Namespaces `Redshift.*`, un asmdef par dossier Scripts.

### 6.3 Patterns imposés
- **Data-driven par ScriptableObjects** : aucun nombre d'équilibrage en dur dans le code.
- **Machine à états de partie** : `GamePhase { Ftl, Arrival, Exploration, Critical, Collapse, Jump }` pilotée host-side, répliquée.
- **Event bus léger** (C# events statiques typés ou ScriptableObject events) pour découpler UI/audio/gameplay.
- **Poches d'intérieur** : prefab de ruine instancié à `(index × 2000, -1000, 0)` hors du volume du système ; paire de portails `PortalLink` (téléport joueur + objets tenus + ragdolls, transition masquée par sas).
- **Physique** : loot en rigidbody endormi, éveillé à l'interaction ; navettes en `Rigidbody` 6DOF ; joueurs en rigidbody kinématique-hybride aligné gravité.

### 6.4 Performance (cibles)
60 fps à 4 joueurs sur GPU classe GTX 1060 / RX 580 (public stream). Budget : < 1500 draw calls (batching statique + GPU instancing des props), matériaux unis partagés, ombres temps réel limitées à la lumière stellaire + lampes joueurs, LOD simples sur planètes. Profiling à chaque gate via les tools `profiler-*` du MCP.

---

## 7. Direction artistique & audio (mitigation « visuel complexe »)

- **Low-poly flat-shaded**, matériaux unis, **zéro texture détaillée**. La lisibilité vient de la silhouette et de la palette.
- Palette par biome + **la teinte globale du système vire au rouge** avec les phases de l'étoile (la DA EST le timer).
- 3 shaders signature (le budget « wow ») : étoile/supernova (émissif + granulation + expansion), hologrammes (scan, carte), dissolution (objets touchés par l'onde).
- Skybox nébuleuse peinte (2-3 variantes).
- Audio : le grondement de l'étoile monte avec les phases (mix layers) ; silence spatial à l'extérieur des planètes cassé par la radio ; SFX monstres reconnaissables à l'aveugle. Musique : ambient drone, sting au passage de phase. (Assets : banques libres + Audacity ; compositeur = dépense post-validation.)

---

## 8. Contenu : MVP vs 1.0

| Contenu | MVP (fin P5) | 1.0 (fin P7) |
|---------|--------------|--------------|
| Systèmes templates | 1 | 5 (+ système final scripté) |
| Planètes templates | Ancrage + 3 | 8 |
| Biomes | 2 (Roche, Glace) | 4 |
| Kits de ruines | 1 (2 layouts fixes) | 3 kits modulaires seedés |
| Monstres | 3 | 7 |
| Items | ~15 | 40+ |
| Upgrades | 6 | ~25 + Drive 6 niveaux |
| Événements | 2 | 6+ |
| Lore | 5 fragments | 40+ fragments, 3 épilogues |
| Fin de jeu | — | Système final + épilogues |

---

## 9. Roadmap phasée — gates bloquants

Règle : **ne jamais entamer la phase N+1 sans validation humaine explicite du gate N.** Chaque gate = critères binaires + tests verts + build qui tourne.

### P0 — Fondations (~1 sem)
Projet Unity 6 LTS + URP (chemin **sans espaces** — requis Unity-MCP), git + .gitignore Unity, FishNet + FishySteamworks importés, Unity-MCP + extensions (ProBuilder, ParticleSystem, Animation) installés et connectés à Claude Code, asmdefs, scène `Sandbox`, CI locale minimale (script de build).
**Gate G0** : le projet compile sans erreur ; Claude Code crée un GameObject via MCP, lance un test EditMode trivial via `tests-run`, lit la console via `console-get-logs`, prend un screenshot.

### P1 — Marcher sur une planète (~1-2 sem)
Gravité sphérique, controller FPS aligné gravité (réseau-aware dès l'écriture), interaction/pickup/slots, planète test 200 m greyboxée (ProBuilder), lampe + scanner v1.
**Gate G1** : faire le tour complet d'une planète de 200 m en marchant/sautant sans glitch d'orientation ni de caméra ; transition propre entre 2 champs de gravité ; ramasser/poser/lâcher des objets ; tests PlayMode gravité verts.

### P2 — Voler entre les planètes (~2 sem)
Navette 6DOF assistée, sièges (parentage + ownership), atterrissage auto-snap, dégâts coque, système test à 3 planètes + étoile placeholder, micro-gravité hors champ.
**Gate G2** : décoller de la planète A, voler, se poser sur B, en sortir et marcher, revenir — en boucle, à deux joueurs en LAN/Steam si possible sinon solo + validation réseau en G4 ; aucune perte d'alignement gravité ; crash de navette produit épave + éjection.

### P3 — Boucle système jouable solo (~3-4 sem) — *vertical slice solo*
`GamePhase` FSM + timer + StarDirector v1 (teinte + échelle), quota + soute + dépôt, minage (filon + laser + chaleur + aggro sonore stub), 1 ruine (poche + portails + NavMesh), monstres Fouisseur + Pâle, mort/spectateur, HUD minimal, onde de choc qui tue, saut de fin de système vers un écran récap.
**Gate G3** : une run solo complète de 20-25 min : atterrir, miner, fouiller la ruine, se faire chasser, déposer le quota, décoller à temps OU mourir dans la supernova — sans bug bloquant ; tests EditMode économie/quota/timer verts ; 60 fps.

### P4 — Multijoueur réel (~3-4 sem) — *phase à plus haut risque*
Lobby Steam, sync complète (joueurs, navettes+sièges, loot physique, monstres, portails, phases), Vivox positionnel, Nuée (3e monstre, extérieur), reconnexion, canal morts.
**Gate G4** : une run complète à 3-4 joueurs via Steam (pas LAN) sans desync visible sur : objets tenus, sièges, portails de ruine, quota, timer ; voice positionnel fonctionnel ; 3 sessions de playtest externes consécutives sans crash host.

### P5 — Boucle méta (~2-3 sem) — *= MVP complet*
Phase FTL (Arche praticable), vente/shop/upgrades (6), décraft, Cœurs d'intégrité, carte stellaire 3-routes, quota croissant, save/load JSON par expédition, 2e biome (Glace) + 2e layout de ruine.
**Gate G5** : une expédition de 3 systèmes enchaînés à 2+ joueurs avec save/reload entre les systèmes ; l'économie boucle (on peut rater un quota et continuer) ; **décision GO/NO-GO produit** : si le fun n'est pas là à G5, on pivote ou on arrête — pas après.

### P6 — Contenu (~6-8 sem)
Biomes 3-4, kits de ruines modulaires seedés, monstres 4-7, événements 6+, upgrades ~25, Drive 6 niveaux + Fragments + Sentinelle, lore 40+, système final + épilogues, DA finale (shaders signature, skyboxes), audio complet.
**Gate G6** : expédition complète 6-8 systèmes jusqu'à la fin, à 4 joueurs ; toutes les tables de contenu remplies ; pas de placeholder visible.

### P7 — Polish & Early Access (~4-6 sem)
Juice (screenshake, ragdolls, feedbacks), onboarding (1er système tutorialisé), options/accessibilité/rebind, localisation EN+FR, optimisation finale, page Steam + trailer (captures du money shot supernova), Steam Direct, playtests fermés puis démo Next Fest si le calendrier s'y prête.
**Gate G7** : release EA.

**Estimation honnête** : solo + Claude Code, en parallèle de la formation : MVP (G5) ≈ 3-4 mois, EA ≈ 8-12 mois. Le négatif « long à dev » ne se contourne pas, il se gère : G5 est le point de sortie à moindre coût.

---

## 10. Risques & mitigations

| Risque | Impact | Mitigation |
|--------|--------|------------|
| Physique multi (sièges, objets tenus, portails) | Élevé | D5 (joueurs assis), D6 (poches), loot endormi, autorité host, P4 dédiée avec gate dur |
| Gravité sphérique × netcode | Élevé | Owner-authoritative mouvement, tests PlayMode dédiés dès P1, pas d'orbites (D4) |
| Scope creep contenu | Élevé | Tableau §8 contractuel ; tout ajout passe par une ligne « post-1.0 » |
| Le fun n'émerge pas | Moyen | GO/NO-GO explicite à G5 ; playtests externes dès G4 |
| Vivox (quota, replays voix) | Moyen | Fallback Dissonance ; l'Écho est gaté en R&D post-1.0 |
| NavMesh/IA sur sphère | Éliminé | D6 : les monstres intelligents vivent dans les poches ; extérieur = steering simple |
| Précision flottante spatiale | Éliminé | D3 : système ≤ 8 km, pas de floating origin |
| Marché saturé co-op horror | Moyen | Différenciateurs réels : pilotage, planètes sphériques, timer supernova spectaculaire |

---

## 11. Business (le « positiv »)

Créneau prouvé : Lethal Company, Content Warning, R.E.P.O. — le co-op extraction streamable à petit prix est la catégorie indie au meilleur ratio coût/probabilité de hit, portée par les clips. Différenciateur marketing en une phrase : *« Lethal Company, mais la deadline est une supernova et vous pilotez pour y échapper. »* Le trailer se construit autour du money shot (§4.10). Prix 9,99 € EA, wishlist via démo + Next Fest, cible 7k wishlists avant EA. Budget cash : Steam Direct 100 $, éventuellement banques audio/compositeur post-G5.

## 12. Questions ouvertes (non bloquantes avant P3)
1. Nom définitif (REDSHIFT est disponible en vibe, à vérifier en trademark/Steam).
2. Nombre de navettes vs navette 4 places unique (test fun à G4).
3. Friendly fire (proposition : off par défaut, option lobby).