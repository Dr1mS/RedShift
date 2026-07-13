using UnityEngine.SceneManagement;

/* REDSHIFT compatibility patch — Unity 6000.5 turned the SceneHandle<->int
 * implicit conversions and Object.GetInstanceID() into obsolete-as-error,
 * which breaks FishNet 4.7.2 as shipped. Patched call sites use these
 * extensions instead. Remove this file (and the HandleInt/InstanceIdInt
 * call sites) once FishNet ships native Unity 6000.5 support.
 * Global namespace on purpose so patched files need no extra using. */
public static class RedshiftUnity6500Compat
{
    /// <summary>Legacy int scene handle (low 32 bits of the SceneHandle raw data).</summary>
    public static int HandleInt(this Scene scene) => unchecked((int)scene.handle.GetRawData());

    /// <summary>Legacy int instance id; negative still means runtime-instantiated.</summary>
    public static int InstanceIdInt(this UnityEngine.Object obj) => unchecked((int)UnityEngine.EntityId.ToULong(obj.GetEntityId()));
}
