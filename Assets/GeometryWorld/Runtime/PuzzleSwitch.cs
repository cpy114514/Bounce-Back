using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class PuzzleSwitch : MonoBehaviour
    {
        public PuzzleSequence onInteract;
        public bool oneShot;
        private bool used;

        public void Interact()
        {
            if (oneShot && used)
                return;
            used = true;
            if (onInteract)
                onInteract.Run();
        }
    }
}
