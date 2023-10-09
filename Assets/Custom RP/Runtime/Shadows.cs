using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{

    const string bufferName = "Shadows";

    // 限制阴影灯光数量,级联的级数
    const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;// 裁剪信息

    ShadowSettings settings;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    int ShadowedDirectionalLightCount;// 记录阴影灯光数量

    ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        //shadowDistanceId = Shader.PropertyToID("_ShadowDistance");
        // 使用渐变距离
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];

    static Matrix4x4[]// 阴影的变换矩阵
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount];

    public Vector2 ReserveDirectionalShadows(Light light, int visibleLightIndex)// 返回阴影贴图的偏移位置 
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount && // 数量不超过最大数量
            light.shadows != LightShadows.None && light.shadowStrength > 0f &&// 阴影模式不为无且阴影强度大于零
            cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))// 检查是否有可影响的物体
        {
            ShadowedDirectionalLights[ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex
                };
            return new Vector2(
                light.shadowStrength,
                settings.directional.cascadeCount * ShadowedDirectionalLightCount++
            );
        }
        return Vector2.zero;
    }

    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(
                dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
            );
        }
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;


        // 获得渲染纹理
        buffer.GetTemporaryRT(
            dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
        );

        // 指示 GPU 渲染此纹理
        buffer.SetRenderTarget(
            dirShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );

        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(
            cascadeCullingSpheresId, cascadeCullingSpheres
        );

        // 设置全局矩阵数组
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        // 设置最大距离 在级联间平滑过渡
        //buffer.SetGlobalFloat(shadowDistanceId, settings.maxDistance);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId, new Vector4(
                1f / settings.maxDistance, 1f / settings.distanceFade,
                1f / (1f - f * f)
            )
        );

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)// 是否使用了反转的ZBuffer
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        // 将矩阵范围 约束到 0-1
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    void SetTileViewport(int index, int split)
    {
        Vector2 offset = new Vector2(index % split, index / split);
    }
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));
        return offset;
    }

    // 渲染阴影
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(// 配置settings
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Orthographic
        );

        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, 0f,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );

            shadowSettings.splitData = splitData;

            if (index == 0)// 记录记录级联剔除球,只记录一个光源的即可(因为公用)
            {
                Vector4 cullingSphere = splitData.cullingSphere;
                cullingSphere.w *= cullingSphere.w;
                cascadeCullingSpheres[i] = cullingSphere;
            }

            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), split
            );
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
        }

    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }

    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings
    )
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;

        ShadowedDirectionalLightCount = 0;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}