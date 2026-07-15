using System.Collections.Generic;
using UnityEngine;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Un stimulus d'aggro perçu par l'essaim (SPEC §4.8) : position monde + force (0-1).
    /// Bruit de minage (transitoire) et lampe allumée (persistante) sont fusionnés en une
    /// liste homogène pour le choix de cible. Struct pure, aucun lien scène.
    /// </summary>
    public readonly struct SwarmStimulus
    {
        public readonly Vector3 Position;
        public readonly float Strength;

        public SwarmStimulus(Vector3 position, float strength)
        {
            Position = position;
            Strength = strength;
        }
    }

    /// <summary>
    /// Cœur logique pur de la Nuée (SPEC §4.8) : steering aligné gravité, projection d'altitude
    /// sur la sphère locale, dynamique d'aggro (montée/décroissance/hystérésis) et choix de cible
    /// entre stimuli. Aucune dépendance UnityEngine.Object → testable en EditMode (calque
    /// <see cref="CreaturePerception"/>). L'environnement (up local, positions, tunables) est
    /// TOUJOURS passé en paramètre : le MonoBehaviour fournit l'up depuis GravityReceiver.
    /// </summary>
    public static class SwarmSteering
    {
        /// <summary>
        /// Nouvelle vitesse du centre de l'essaim après un pas de steering « seek » vers
        /// <paramref name="target"/>, avec rappel doux vers l'altitude cible le long de
        /// <paramref name="localUp"/> et clamp d'accélération puis de vitesse. Logique pure :
        /// <paramref name="localUp"/> est une ENTRÉE normalisée (opposé de la gravité locale).
        /// </summary>
        /// <param name="position">Position actuelle du centre.</param>
        /// <param name="velocity">Vitesse actuelle du centre.</param>
        /// <param name="target">Point recherché (errance ou joueur harcelé).</param>
        /// <param name="localUp">Haut local (normalisé). Vector3.up par défaut hors gravité.</param>
        /// <param name="desiredAltitude">Altitude à tenir au-dessus de la surface le long de localUp.</param>
        /// <param name="groundHeightAlongUp">Hauteur du sol projetée sur localUp au XY courant (offset scalaire).</param>
        /// <param name="currentHeightAlongUp">Hauteur actuelle du centre projetée sur localUp.</param>
        /// <param name="maxSpeed">Vitesse max (m/s).</param>
        /// <param name="maxAccel">Accélération max sur ce pas (m/s²).</param>
        /// <param name="altitudeStiffness">Raideur du rappel d'altitude (1/s).</param>
        /// <param name="dt">Pas de temps (s).</param>
        public static Vector3 ComputeVelocity(
            Vector3 position,
            Vector3 velocity,
            Vector3 target,
            Vector3 localUp,
            float desiredAltitude,
            float groundHeightAlongUp,
            float currentHeightAlongUp,
            float maxSpeed,
            float maxAccel,
            float altitudeStiffness,
            float dt)
        {
            if (dt <= 0f)
                return velocity;

            Vector3 up = localUp.sqrMagnitude > 1e-6f ? localUp.normalized : Vector3.up;

            // Direction désirée : composante tangentielle (vers la cible, à plat sur la surface)
            // + composante verticale (rappel d'altitude le long de l'up local).
            Vector3 toTarget = target - position;
            Vector3 tangential = Vector3.ProjectOnPlane(toTarget, up);

            float altitudeError = (groundHeightAlongUp + desiredAltitude) - currentHeightAlongUp;
            Vector3 desiredVel = Vector3.zero;
            if (tangential.sqrMagnitude > 1e-6f)
                desiredVel += tangential.normalized * maxSpeed;
            // Rappel d'altitude proportionnel, borné à maxSpeed (évite les plongeons violents).
            desiredVel += up * Mathf.Clamp(altitudeError * altitudeStiffness, -maxSpeed, maxSpeed);

            if (desiredVel.sqrMagnitude > maxSpeed * maxSpeed)
                desiredVel = desiredVel.normalized * maxSpeed;

            // Accélération bornée vers la vitesse désirée (virages lisses, pas de snap).
            Vector3 accel = (desiredVel - velocity) / dt;
            if (accel.sqrMagnitude > maxAccel * maxAccel)
                accel = accel.normalized * maxAccel;

            Vector3 newVel = velocity + accel * dt;
            if (newVel.sqrMagnitude > maxSpeed * maxSpeed)
                newVel = newVel.normalized * maxSpeed;
            return newVel;
        }

        /// <summary>
        /// Fait avancer la valeur d'aggro (0-1) d'un pas et renvoie l'état d'engagement avec
        /// HYSTÉRÉSIS : monte vers <paramref name="stimulusStrength"/> à <paramref name="buildRate"/>
        /// quand un stimulus est perçu, décroît à <paramref name="decayRate"/> sinon (contre-jeu
        /// « sessions courtes »). S'engage au-dessus de <paramref name="engageThreshold"/>, se
        /// désengage seulement sous <paramref name="disengageThreshold"/> (&lt; engage).
        /// </summary>
        /// <param name="aggro">Aggro courante (0-1).</param>
        /// <param name="wasEngaged">État d'engagement au tick précédent (pour l'hystérésis).</param>
        /// <param name="stimulusStrength">Force du meilleur stimulus courant (0-1), 0 si aucun.</param>
        public static float UpdateAggro(
            float aggro,
            bool wasEngaged,
            float stimulusStrength,
            float buildRate,
            float decayRate,
            float engageThreshold,
            float disengageThreshold,
            float dt,
            out bool engaged)
        {
            if (dt > 0f)
            {
                if (stimulusStrength > aggro)
                    aggro = Mathf.MoveTowards(aggro, stimulusStrength, buildRate * dt);
                else
                    aggro = Mathf.MoveTowards(aggro, 0f, decayRate * dt);
            }
            aggro = Mathf.Clamp01(aggro);

            // Hystérésis : une fois engagé on le reste jusqu'à passer sous le seuil bas.
            if (wasEngaged)
                engaged = aggro > disengageThreshold;
            else
                engaged = aggro >= engageThreshold;

            return aggro;
        }

        /// <summary>
        /// Choisit l'index du stimulus le plus « fort d'abord, proche ensuite » (SPEC : l'essaim
        /// converge vers la source de bruit/lumière). Départage par force décroissante puis par
        /// distance croissante à <paramref name="swarmPosition"/>. Retourne -1 si aucun stimulus.
        /// Calque <see cref="CreaturePerception.SelectStalkTarget"/> (logique pure, indices).
        /// </summary>
        public static int SelectTarget(IReadOnlyList<SwarmStimulus> stimuli, Vector3 swarmPosition)
        {
            if (stimuli == null || stimuli.Count == 0)
                return -1;

            const float strengthEpsilon = 0.01f;
            int best = -1;
            float bestStrength = 0f;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < stimuli.Count; i++)
            {
                float strength = stimuli[i].Strength;
                if (strength <= 0f)
                    continue;
                float sqr = (stimuli[i].Position - swarmPosition).sqrMagnitude;

                bool better;
                if (best < 0)
                    better = true;
                else if (strength > bestStrength + strengthEpsilon)
                    better = true; // nettement plus fort → gagne
                else if (strength < bestStrength - strengthEpsilon)
                    better = false; // nettement plus faible → perd
                else
                    better = sqr < bestSqr; // force ~égale → le plus proche

                if (better)
                {
                    bestStrength = strength;
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }
    }
}
