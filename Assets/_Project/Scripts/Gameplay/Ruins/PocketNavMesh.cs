using UnityEngine;
using UnityEngine.AI;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Enregistre au runtime le NavMesh pré-baké d'une poche de ruine (SPEC D6).
    /// Le bake est fait en éditeur via NavMeshBuilder (module AI intégré,
    /// pas de dépendance com.unity.ai.navigation) et stocké en asset.
    /// </summary>
    public class PocketNavMesh : MonoBehaviour
    {
        [SerializeField] private NavMeshData _data;

        private NavMeshDataInstance instance;

        private void OnEnable()
        {
            if (_data != null)
                instance = NavMesh.AddNavMeshData(_data);
        }

        private void OnDisable() => instance.Remove();
    }
}
