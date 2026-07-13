using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Redshift.Gameplay
{
    /// <summary>
    /// Scanner v1 : ping local qui surligne les <see cref="ScannableTarget"/> dans un rayon.
    /// Purement cosmétique côté propriétaire — rien à répliquer.
    /// </summary>
    public class Scanner : MonoBehaviour
    {
        [SerializeField] private ScannerDef _def;
        [SerializeField] private InputActionAsset _inputAsset;

        private static Material markerMaterial;

        private InputAction scanAction;
        private float nextScanTime;
        private Collider[] hits;

        private void OnEnable()
        {
            scanAction = _inputAsset.FindActionMap("Player", throwIfNotFound: true).FindAction("Scan", throwIfNotFound: true);
            hits = new Collider[_def.MaxTargets];
        }

        private void Update()
        {
            if (!scanAction.WasPressedThisFrame() || Time.time < nextScanTime)
                return;
            nextScanTime = Time.time + _def.Cooldown;
            Ping();
        }

        private void Ping()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, _def.Radius, hits, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<ScannableTarget>();
            for (int i = 0; i < count; i++)
            {
                ScannableTarget target = hits[i].GetComponentInParent<ScannableTarget>();
                if (target == null || !seen.Add(target))
                    continue;
                SpawnMarker(target);
            }
        }

        private void SpawnMarker(ScannableTarget target)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "ScanMarker";
            Object.Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(target.transform, worldPositionStays: false);
            marker.transform.localScale = Vector3.one * 0.35f;
            marker.GetComponent<Renderer>().sharedMaterial = GetMarkerMaterial();
            Object.Destroy(marker, _def.MarkerDuration);
        }

        private static Material GetMarkerMaterial()
        {
            if (markerMaterial == null)
            {
                markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                {
                    color = new Color(0.3f, 1f, 0.9f, 1f),
                };
            }
            return markerMaterial;
        }
    }
}
