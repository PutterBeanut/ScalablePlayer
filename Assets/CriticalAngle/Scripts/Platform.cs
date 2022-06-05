using UnityEngine;

namespace CriticalAngle.ExpandablePlayer
{
    public class Platform : MonoBehaviour
    {
        public Vector3 Velocity { get; private set; }

        private Vector3 previousPosition;
        
        private void Update()
        {
            var position = this.transform.position;
            this.Velocity = position - this.previousPosition;
            this.previousPosition = position;
        }
    }
}