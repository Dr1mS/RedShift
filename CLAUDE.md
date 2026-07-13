# CLAUDE.md — REDSHIFT

Fichier opérationnel pour Claude Code. La source de vérité design/technique est `REDSHIFT_SPEC.md` — le lire avant toute tâche. En cas de conflit : SPEC > CLAUDE.md > jugement.

## Contexte
Jeu co-op 1-4 joueurs FPS extraction : « Lethal Company × Outer Wilds ». Unity 6 LTS, URP, FishNet + FishySteamworks, développement piloté par phases avec gates bloquants (SPEC §9). Propriétaire : Adrien (Dr1mS). Communication en français, code et identifiants en anglais.

## Règles absolues
1. **Gate discipline** : ne jamais commencer une tâche de la phase N+1 tant que le gate N n'a pas été validé explicitement par Adrien. Si une tâche demandée semble hors phase, le signaler avant d'agir.
2. **Pas de procgen de terrain** (SPEC D7). Randomisation = spawns/loot/événements uniquement.
3. **Pas d'asset payant** ni de dépendance nouvelle sans validation.
4. **Pas de refactor massif non demandé.** Une tâche = un périmètre.
5. **Network-aware dès l'écriture** : tout système gameplay naît en `NetworkBehaviour` avec autorité définie (SPEC §5), même testé en solo.
6. **Aucun nombre d'équilibrage en dur** : tout passe par les ScriptableObjects de `Assets/_Project/Data/`.
7. Chemin du projet Unity **sans espaces** (requis Unity-MCP).

## Environnement & setup

```bash
# Setup initial (P0) — unity-mcp-cli
npm install -g unity-mcp-cli
unity-mcp-cli install-plugin ./Redshift
unity-mcp-cli login ./Redshift
unity-mcp-cli open ./Redshift
unity-mcp-cli wait-for-ready ./Redshift
# Puis dans Unity : Window/AI Game Developer → Auto-generate skills (client : claude-code)
```

Extensions Unity-MCP à installer en P0 (via `package-add`, Git URL) :
- `Unity-AI-ProBuilder` (IvanMurzak) — greybox planètes, ruines, Arche
- `Unity-AI-ParticleSystem` — VFX supernova, laser de minage, propulseurs
- `Unity-AI-Animation` — monstres, portes, séquences

Packages projet : FishNet (Asset Store/GitHub), FishySteamworks + Facepunch.Steamworks, Vivox (UGS), Input System, Cinemachine, ProBuilder, UniTask.

## Répartition des rôles Claude Code ↔ Unity-MCP

**Code C#** : éditer les fichiers directement (outils fichiers de Claude Code, git-friendly). Ne pas utiliser `script-update-or-create` pour du code versionné sauf création rapide de stub. Après toute modif C# : attendre la recompilation, puis vérifier `console-get-logs` (zéro erreur, zéro warning nouveau).

**Éditeur Unity** (via MCP) :
- Scènes/hiérarchie : `scene-*`, `gameobject-*` (création, composants, parentage)
- Assets : `assets-*` (matériaux, prefabs — `assets-prefab-create/open/save`, dossiers, recherche)
- Validation : `editor-application-set-state` (playmode), `console-get-logs`, `tests-run`
- Vérification visuelle : `screenshot-game-view`, `screenshot-scene-view`, `screenshot-isolated` (props/monstres sous 4 angles)
- Diagnostic : `profiler-*` à chaque gate (cible : 60 fps, budget SPEC §6.4)
- Ponctuel : `script-execute` (Roslyn) pour inspecter/manipuler l'état de l'éditeur, `reflection-method-call` pour les cas exotiques — jamais pour du gameplay persistant

## Conventions

- **C#** : PascalCase public, camelCase privé sans underscore préfixe sauf `_camelCase` pour les champs sérialisés privés — choisir UNE convention en P0 et la figer dans `.editorconfig`. Namespaces `Redshift.Core / .Gameplay / .Networking / .Meta / .UI`. Un asmdef par dossier.
- **Unity** : prefabs préfixés par type (`P_`, `SO_`, `MAT_`, `VFX_`), scènes en `SCN_`. Pas de GameObject nommé « GameObject (1) » commité.
- **Data** : un ScriptableObject par entité (`SO_Item_ScrapPlate`, `SO_Creature_Pale`, `SO_System_T1_Roche`...), rangé par type dans `Data/`.
- **Git** : commits conventionnels (`feat:`, `fix:`, `chore:`, `test:`), une feature = une branche, merge sur `main` uniquement avec DoD remplie. `.gitignore` Unity standard + `Library/mcp-server/`.
- **Tests** : `Tests/EditMode` pour la logique pure (quota, économie, tirages, FSM), `Tests/PlayMode` pour gravité/vol/interactions/portails. Toute logique de règles nouvelle arrive avec son test EditMode.

## Definition of Done (chaque tâche)
1. Compile sans erreur ni warning nouveau (`console-get-logs` propre).
2. Tests concernés verts (`tests-run`), nouveaux tests si logique de règles.
3. Si visuel : screenshot pris et montré (`screenshot-game-view` / `screenshot-isolated`).
4. Si gameplay : validé en playmode (`editor-application-set-state`) au moins une fois.
5. Données d'équilibrage dans des SO, pas dans le code.
6. Commit conventionnel avec description courte de ce qui a été validé.

## Validation d'un gate (SPEC §9)
Produire un rapport court : critères du gate un par un avec preuve (résultat `tests-run`, screenshots, mesures `profiler-get-rendering-stats`), liste des dettes connues. Adrien tranche GO/NO-GO. Ne rien anticiper de la phase suivante en attendant.

## Pièges connus du projet (lire avant d'implémenter)
- **Gravité** : hystérésis obligatoire à la frontière entre deux `GravitySource` (SPEC §4.2) ; la caméra suit l'up local en slerp, jamais en snap.
- **Sièges** : parentage réseau joueur↔siège = transfert d'ownership de la navette au pilote AVANT le parentage, sinon fight d'autorité.
- **Poches de ruines** : positions fixes réservées `(index × 2000, -1000, 0)` ; les portails téléportent joueur + objets tenus + ragdoll dans le même tick ; jamais de physique active pendant le téléport.
- **Loot physique** : rigidbodies endormis par défaut, réveil à l'interaction, autorité host ; ne jamais répliquer un rigidbody éveillé en continu à 4 joueurs sans nécessité.
- **Échelle** : rien au-delà de 8 km de l'origine, jamais. Pas de floating origin dans ce projet.
- **Étoile** : c'est un shader + un directeur (`StarDirector`), pas un objet physique ; l'onde de choc est le seul collider létal.