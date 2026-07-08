namespace TextEngine
{
    using UnityEngine;
    using TMPro;

    [RequireComponent(typeof(TextMeshProUGUI))]
    public class WobblyText : MonoBehaviour
    {
        public float wobbleIntensity = 0.0f; 
        public float wobbleSpeed = 10.0f;
        public float wobbleHeight = 5.0f;
        private TextMeshProUGUI textMesh;
        private Mesh mesh;
        private Vector3[] vertices;

        void Awake()
        {
            textMesh = GetComponent<TextMeshProUGUI>();
        }

        void Update()
        {
            // We only run the effect if there's some intensity
            if (wobbleIntensity == 0.0f)
            {
                return;
            }
            textMesh.ForceMeshUpdate();
            mesh = textMesh.mesh;
            vertices = mesh.vertices;
            for (int i = 0; i < textMesh.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textMesh.textInfo.characterInfo[i];
                // Skip invisible characters
                if (!charInfo.isVisible)
                {
                    continue;
                }
                // Get the vertex indices for this character
                int vertexIndex = charInfo.vertexIndex;
                // Apply a sine wave offset to the Y position of each vertex in the character
                Vector3 offset = new Vector3(0, Mathf.Sin(Time.time * wobbleSpeed + i) * wobbleHeight * wobbleIntensity, 0);
                vertices[vertexIndex + 0] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
            }
            // Apply the modified vertices back to the mesh
            mesh.vertices = vertices;
            textMesh.canvasRenderer.SetMesh(mesh);
        }
    }
}
