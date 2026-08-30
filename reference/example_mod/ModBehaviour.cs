using UnityEngine;

namespace ExampleMod
{
    /// <summary>
    /// Minimal mod: logs on load. The game loads <c>ExampleMod.ModBehaviour</c>
    /// from <c>ExampleMod.dll</c> based on info.ini's name field.
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        void Awake()
        {
            Debug.Log("ExampleMod loaded!");
        }
    }
}
