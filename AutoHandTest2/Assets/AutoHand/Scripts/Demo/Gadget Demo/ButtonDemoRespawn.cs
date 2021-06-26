using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Autohand.Demo{
    public class ButtonDemoRespawn : MonoBehaviour{
        public Transform root;

        List<Transform> respawns = new List<Transform>();

        Vector3[] startPos;
        Quaternion[] startRot;


        void Start(){

            for (int i = 0; i < root.childCount; i++){
                respawns.Add(root.GetChild(i));
            }

            startPos = new Vector3[respawns.Count];
            startRot = new Quaternion[respawns.Count];
            for(int i = 0; i < respawns.Count; i++) {
                startPos[i] = respawns[i].transform.position;
                startRot[i] = respawns[i].transform.rotation;
            }
        }

        public void Respawn() {
            for(int i = 0; i < respawns.Count; i++) {
                try {
                    if (respawns[i].CanGetComponent(out Rigidbody body)){
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                        body.ResetInertiaTensor();
                    }
                    respawns[i].transform.position = startPos[i];
                    respawns[i].transform.rotation = startRot[i];
                }
                catch { }
            }
        }

        public void ReloadScene() {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
}
}