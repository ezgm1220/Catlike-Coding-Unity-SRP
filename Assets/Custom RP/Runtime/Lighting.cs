using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{

    const string bufferName = "Lighting";

    //static int // 跟踪两个着色器属性的标识符
    //    dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
    //    dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
    const int maxDirLightCount = 4;// 灯光最大数量

    // 配置新的属性标识符
    static int
        //dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
        //dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");

    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount];


    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cullingResults;

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

        int dirLightCount = 0;

        for (int i = 0; i < visibleLights.Length; i++)// 遍历光源
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)// 当光源为方向光时
            {
                SetupDirectionalLight(dirLightCount++, ref visibleLight);// 传递光源数据
                if (dirLightCount >= maxDirLightCount)// 使方向光数量不超过定义的数量
                {
                    break;
                }
            }
        }

        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
    }

    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults
    )
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        SetupLights();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
    }
}