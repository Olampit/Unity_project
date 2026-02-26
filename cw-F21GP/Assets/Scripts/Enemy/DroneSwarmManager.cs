using System.Collections.Generic;
using UnityEngine;

namespace F21GP.Enemy
{
    public class DroneSwarmManager : MonoBehaviour
    {
        public static List<DroneSwarmManager> AllManagers = new List<DroneSwarmManager>();

        private List<SwarmMember> activeSwarmMembers = new List<SwarmMember>();

        public Vector3 SwarmCenter { get; private set; }
        public Vector3 SwarmHeading { get; private set; }
        public int SwarmCount => activeSwarmMembers.Count;
        
        public SwarmMember Leader { get; private set; }

        private void OnEnable()
        {
            if (!AllManagers.Contains(this))
                AllManagers.Add(this);
        }

        private void OnDisable()
        {
            AllManagers.Remove(this);
        }

        public void RegisterMember(SwarmMember member)
        {
            if (!activeSwarmMembers.Contains(member))
            {
                activeSwarmMembers.Add(member);
                if (Leader == null)
                {
                    Leader = member;
                }
            }
        }

        public void UnregisterMember(SwarmMember member)
        {
            if (activeSwarmMembers.Contains(member))
            {
                activeSwarmMembers.Remove(member);
                if (Leader == member)
                {
                    Leader = activeSwarmMembers.Count > 0 ? activeSwarmMembers[0] : null;
                }
            }
        }

        private void Update()
        {
            if (activeSwarmMembers.Count == 0) return;

            Vector3 totalPosition = Vector3.zero;
            Vector3 totalHeading = Vector3.zero;

            foreach (var member in activeSwarmMembers)
            {
                totalPosition += member.transform.position;
                if (member.Agent.hasPath && member.Agent.velocity.sqrMagnitude > 0.1f)
                {
                    totalHeading += member.Agent.velocity.normalized;
                }
                else
                {
                    totalHeading += member.transform.forward;
                }
            }

            SwarmCenter = totalPosition / activeSwarmMembers.Count;
            SwarmHeading = (totalHeading / activeSwarmMembers.Count).normalized;
        }
    }
}
