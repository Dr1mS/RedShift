using UnityEngine;
using UnityEngine.AI;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Enregistre au runtime le NavMesh pré-baké d'une poche de ruine (SPEC D6).
    /// Le bake est fait en éditeur via NavMeshBuilder (module AI intégré,
    /// pas de dépendance com.unity.ai.navigation) et stocké en asset.
    /// Enregistrement en Awake (pas OnEnable) et DefaultExecutionOrder négatif :
    /// on veut la NavMeshData enregistrée le plus tôt possible dans le cycle de
    /// chargement de la scène. Note : la cause racine du bug build (« Failed to
    /// create agent because there is no valid NavMesh », 2 occurrences en
    /// standalone, 0 en éditeur) n'était pas l'ordre Awake/OnEnable des scripts
    /// mais l'attache native du NavMeshAgent des créatures à la désérialisation
    /// de scène — avant même le premier Awake scripté. Le vrai fix est
    /// l'override enabled=false sur les instances de scène des créatures
    /// (voir Creature.Awake/OnStartServer) ; le passage Awake/OnDestroy ici
    /// reste une défense en profondeur saine (enregistrement au plus tôt,
    /// retrait symétrique — Awake ne re-tourne jamais donc Remove doit vivre
    /// en OnDestroy et non OnDisable pour éviter un NavMesh manquant après un
    /// toggle d'activation).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PocketNavMesh : MonoBehaviour
    {
        [SerializeField] private NavMeshData _data;

        private NavMeshDataInstance instance;

        private void Awake()
        {
            if (_data != null)
                instance = NavMesh.AddNavMeshData(_data);
        }

        private void OnDestroy() => instance.Remove();
    }
}
