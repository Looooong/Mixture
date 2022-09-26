using GraphProcessor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mixture
{
  [System.Serializable, NodeMenuItem("Subgraph")]
  public class SubgraphNode : MixtureNode, ICreateNodeFrom<CustomRenderTexture>
  {
    // Placeholders for custom port behaviours
    [Input, System.NonSerialized] public int subgraphInputs;
    [Output, System.NonSerialized] public int subgraphOutputs;

    public MixtureGraph subgraph;
    public CustomRenderTexture subgraphTexture;
    public int previewOutputIndex = -1;

    List<CustomRenderTexture> _outputTextures = new List<CustomRenderTexture>();

    public override string name => subgraph?.name ?? "Subgraph";
    public override bool isRenamable => true;
    public override bool hasPreview => -1 < previewOutputIndex && previewOutputIndex < _outputTextures.Count;
    public override Texture previewTexture => hasPreview ? _outputTextures[previewOutputIndex] : null;

    public bool InitializeNodeFromObject(CustomRenderTexture texture)
    {
      subgraph = MixtureDatabase.GetGraphFromTexture(texture);

      if (subgraph == null) return false;

      subgraphTexture = texture;
      previewOutputIndex = Mathf.Clamp(previewOutputIndex, 0, subgraph.outputNode.outputTextureSettings.Count - 1);

      return true;
    }

    [CustomPortBehavior(nameof(subgraphInputs))]
    public IEnumerable<PortData> ListGraphInputs(List<SerializableEdge> edges)
    {
      if (subgraph == null || subgraph == graph) yield break;

      for (var i = 0; i < subgraph.exposedParameters.Count; i++)
      {
        var parameter = subgraph.exposedParameters[i];

        yield return new PortData
        {
          identifier = System.Convert.ToString(i),
          displayName = parameter.name,
          displayType = parameter.GetValueType(),
          acceptMultipleEdges = false,
        };
      }
    }

    [CustomPortBehavior(nameof(subgraphOutputs))]
    public IEnumerable<PortData> ListGraphOutputs(List<SerializableEdge> edges)
    {
      if (subgraph == null || subgraph == graph) yield break;

      var settings = subgraph.outputNode.outputTextureSettings;
      var textureType = GetSubgraphTextureType();

      for (var i = 0; i < settings.Count; i++)
      {
        yield return new PortData
        {
          identifier = System.Convert.ToString(i),
          displayName = settings[i].name,
          displayType = textureType,
          acceptMultipleEdges = true,
        };
      }
    }

    [CustomPortInput(nameof(subgraphInputs), typeof(object))]
    public void AssignGraphInputs(List<SerializableEdge> edges)
    {
      foreach (var edge in edges)
      {
        var index = System.Convert.ToInt32(edge.inputPortIdentifier);
        var parameter = subgraph.exposedParameters[index];

        switch (edge.passThroughBuffer)
        {
          case float v: parameter.value = CoerceVectorValue(parameter, new Vector4(v, v, v, v)); break;
          case Vector2 v: parameter.value = CoerceVectorValue(parameter, v); break;
          case Vector3 v: parameter.value = CoerceVectorValue(parameter, v); break;
          case Vector4 v: parameter.value = CoerceVectorValue(parameter, v); break;
          default: parameter.value = edge.passThroughBuffer; break;
        }
      }
    }

    [CustomPortOutput(nameof(subgraphOutputs), typeof(object))]
    public void AssignGraphOutputs(List<SerializableEdge> edges)
    {
      foreach (var edge in edges)
      {
        var index = System.Convert.ToInt32(edge.outputPortIdentifier);
        edge.passThroughBuffer = _outputTextures[index];
      }
    }

    public void UpdateOutputTextures()
    {
      ReleaseOutputTextures();

      if (subgraph != null && subgraph != graph) GenerateOutputTextures();
    }

    public void GenerateOutputTextures()
    {
      var settings = subgraph.outputNode.outputTextureSettings;

      _outputTextures.Capacity = Mathf.Max(_outputTextures.Capacity, settings.Count);

      foreach (var setting in settings)
      {
        CustomRenderTexture outputTexture = null;
        UpdateTempRenderTexture(ref outputTexture);
        _outputTextures.Add(outputTexture);
      }
    }

    public void ReleaseOutputTextures()
    {
      foreach (var texture in _outputTextures) texture?.Release();

      _outputTextures.Clear();
    }

    protected override void Enable()
    {
      base.Enable();

      UpdateOutputTextures();
    }

    protected override void Disable()
    {
      base.Disable();

      ReleaseOutputTextures();
    }

    public override bool canProcess => base.canProcess && subgraph != null && subgraph != graph;

    protected override bool ProcessNode(CommandBuffer cmd)
    {
      if (!base.ProcessNode(cmd)) return false;

      MixtureGraphProcessor.RunOnce(subgraph);

      using (var copyCmd = new CommandBuffer { name = $"{graph.name}/{subgraph.name}" })
      {
        for (int i = 0; i < _outputTextures.Count; i++)
        {
          var outputTexture = _outputTextures[i];
          UpdateTempRenderTexture(ref outputTexture);
          copyCmd.Blit(subgraph.outputNode.outputTextureSettings[i].finalCopyRT, outputTexture);
        }

        Graphics.ExecuteCommandBuffer(copyCmd);
      }

      return true;
    }

    System.Type GetSubgraphTextureType()
    {
      var textureDimension = subgraph.settings.GetResolvedTextureDimension(subgraph);

      switch (textureDimension)
      {
        case UnityEngine.Rendering.TextureDimension.Tex2D: return typeof(Texture2D);
        case UnityEngine.Rendering.TextureDimension.Tex3D: return typeof(Texture3D);
        case UnityEngine.Rendering.TextureDimension.Cube: return typeof(Cubemap);
        default: throw new System.Exception($"Texture dimension not supported: {textureDimension}");
      }
    }

    object CoerceVectorValue(ExposedParameter parameter, Vector4 vector)
    {
      switch (parameter.value)
      {
        case float: return vector.x;
        case Vector2: return (Vector2)vector;
        case Vector3: return (Vector3)vector;
        case Vector4: return vector;
        default: throw new System.Exception($"Cannot cast vector to {parameter.GetValueType()}");
      }
    }
  }
}
