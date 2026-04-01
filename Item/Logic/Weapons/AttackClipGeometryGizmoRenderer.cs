using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BBBNexus
{
    public static class AttackClipGeometryGizmoRenderer
    {
        public static void DrawUnified(Transform root, AttackClipGeometryDefinition definition, bool selected)
        {
            if (root == null || definition?.Clips == null)
            {
                return;
            }

            for (int i = 0; i < definition.Clips.Count; i++)
            {
                AttackClipGeometryClipDefinition clip = definition.Clips[i];
                if (clip == null)
                {
                    continue;
                }

                Color clipColor = Color.HSVToRGB(Mathf.Repeat(i * 0.17f + 0.92f, 1f), 0.7f, 1f);
                clipColor.a = selected ? 0.18f : 0.11f;
                DrawClip(root, clip, clipColor, selected, $"C{clip.ComboIndex}");
            }
        }

        public static void DrawSingle(Transform root, AttackClipGeometryClipDefinition clip, bool selected)
        {
            if (root == null || clip == null)
            {
                return;
            }

            Color clipColor = new Color(0.14f, 0.9f, 0.96f, selected ? 0.18f : 0.11f);
            DrawClip(root, clip, clipColor, selected, $"C{clip.ComboIndex}");
        }

        private static void DrawClip(
            Transform root,
            AttackClipGeometryClipDefinition clip,
            Color fillColor,
            bool selected,
            string labelPrefix)
        {
            if (clip.Samples == null)
            {
                return;
            }

            for (int i = 0; i < clip.Samples.Count; i++)
            {
                AttackGeometrySampleDefinition sample = clip.Samples[i];
                if (sample?.Shapes == null)
                {
                    continue;
                }

                DrawSample(root, sample, fillColor, selected, $"{labelPrefix} {Mathf.RoundToInt(Mathf.Clamp01(sample.SweepProgressNormalized) * 100f)}%");
            }
        }

        private static void DrawSample(
            Transform root,
            AttackGeometrySampleDefinition sample,
            Color fillColor,
            bool selected,
            string label)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            for (int i = 0; i < sample.Shapes.Count; i++)
            {
                AttackGeometryShapeDefinition shape = sample.Shapes[i];
                Matrix4x4 matrix = Matrix4x4.TRS(
                    root.TransformPoint((Vector3)shape.LocalPosition),
                    root.rotation * shape.LocalRotation,
                    Vector3.one);

                Gizmos.matrix = matrix;
                Gizmos.color = fillColor;

                switch (shape.ShapeType)
                {
                    case AttackGeometryShapeType.Sphere:
                        DrawSphere(shape, fillColor);
                        break;

                    case AttackGeometryShapeType.Capsule:
                        DrawCapsuleApprox(shape, fillColor);
                        break;

                    case AttackGeometryShapeType.Box:
                    default:
                        DrawBox(shape, fillColor);
                        break;
                }

#if UNITY_EDITOR
                if (selected && i == 0)
                {
                    Handles.color = WithAlpha(fillColor, 0.95f);
                    Handles.Label(root.TransformPoint((Vector3)shape.LocalPosition) + root.up * 0.04f, label);
                }
#endif
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static void DrawSphere(AttackGeometryShapeDefinition shape, Color fillColor)
        {
            float radius = Mathf.Max(0.001f, shape.Radius);
            if (fillColor.a > 0f)
            {
                Gizmos.DrawSphere(Vector3.zero, radius);
            }

            Gizmos.color = WithAlpha(fillColor, Mathf.Min(1f, fillColor.a + 0.24f));
            Gizmos.DrawWireSphere(Vector3.zero, radius);
        }

        private static void DrawCapsuleApprox(AttackGeometryShapeDefinition shape, Color fillColor)
        {
            Vector3 size = new Vector3(
                Mathf.Max(0.001f, shape.Radius * 2f),
                Mathf.Max(shape.Radius * 2f, shape.Height),
                Mathf.Max(0.001f, shape.Radius * 2f));

            if (fillColor.a > 0f)
            {
                Gizmos.DrawCube(Vector3.zero, size);
            }

            Gizmos.color = WithAlpha(fillColor, Mathf.Min(1f, fillColor.a + 0.24f));
            Gizmos.DrawWireCube(Vector3.zero, size);
        }

        private static void DrawBox(AttackGeometryShapeDefinition shape, Color fillColor)
        {
            Vector3 size = (Vector3)shape.HalfExtents * 2f;
            size.x = Mathf.Max(0.001f, size.x);
            size.y = Mathf.Max(0.001f, size.y);
            size.z = Mathf.Max(0.001f, size.z);

            if (fillColor.a > 0f)
            {
                Gizmos.DrawCube(Vector3.zero, size);
            }

            Gizmos.color = WithAlpha(fillColor, Mathf.Min(1f, fillColor.a + 0.24f));
            Gizmos.DrawWireCube(Vector3.zero, size);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
