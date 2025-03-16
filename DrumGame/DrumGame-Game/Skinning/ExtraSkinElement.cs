
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DrumGame.Game.Skinning;

public class ExtraSkinElementData
{
    public SkinTexture Texture;
    public AdjustableSkinData Placement;
    public string Key;
}

public class ExtraSkinElement : AdjustableSkinElement
{
    public override AdjustableSkinData DefaultData() => new()
    {
        Width = 50,
        Height = 50
    };

    Expression<Func<Skin, ExtraSkinElementData>> ReferencePath;
    public override Expression<Func<Skin, AdjustableSkinData>> SkinPathExpression { get; }

    ExtraSkinElementData Data => ReferencePath?.GetOrDefault();

    public override void LayoutChanged()
    {
        base.LayoutChanged();
        RefreshTexture();
    }

    void RefreshTexture()
    {
        ClearInternal();
        var texture = ReferencePath.GetOrDefault()?.Texture;
        if (texture != null)
            AddInternal(texture.MakeSprite());
    }

    public override string OverlayTooltip
    {
        get
        {
            var key = Data?.Key;
            if (key != null)
                return $"<faded>Key: </><brightCyan>{key}</>";
            return null;
        }
    }

    static Expression<Func<Skin, ExtraSkinElementData>> makePath(Expression<Func<Skin, List<ExtraSkinElementData>>> referencePath, int i)
    {
        var body = Expression.Property(referencePath.Body, "Item", Expression.Constant(i));
        return Expression.Lambda<Func<Skin, ExtraSkinElementData>>(body, referencePath.Parameters);
    }

    public ExtraSkinElement(Expression<Func<Skin, List<ExtraSkinElementData>>> referencePath, int i) : this(makePath(referencePath, i)) { }
    public ExtraSkinElement(Expression<Func<Skin, ExtraSkinElementData>> referencePath) : base(true)
    {
        ReferencePath = referencePath;
        var body = Expression.Field(referencePath.Body, "Placement");
        SkinPathExpression = Expression.Lambda<Func<Skin, AdjustableSkinData>>(body, referencePath.Parameters);
        InitializeSkinData();
        RefreshTexture();
        SkinManager.RegisterTarget(Data.Key, this);
    }
    protected override void Dispose(bool isDisposing)
    {
        SkinManager.UnregisterTarget(this);
        base.Dispose(isDisposing);
    }
}