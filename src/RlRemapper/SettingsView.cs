using System.ComponentModel;

namespace RlRemapper;

// Presentation only: keep the existing settings file and transform parameters compatible.
internal sealed class SettingsView(Settings settings) : CustomTypeDescriptor
{
    public override PropertyDescriptorCollection GetProperties() => GetProperties(null);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var source = TypeDescriptor.GetProperties(settings);
        var result = new List<PropertyDescriptor>();
        void Add(string category, params string[] names)
        {
            foreach (string name in names) result.Add(new SettingProperty(settings, source[name]!, category));
        }
        Add("Profile", nameof(Settings.MappingMode));
        Add("Radial", "InnerDeadzone", "RadialAntiDeadzone", "RadialMaxZone", "RadialMaxOutput", "SquareStick", "SquareRoundness");
        Add("Axial", "InnerDeadzone", "XSnapWidth", "YSnapWidth", "CenterBlendWidth", "XAntiDeadzone", "XMaxZone", "XMaxOutput", "YAntiDeadzone", "YMaxZone", "YMaxOutput", "SquareStick", "SquareRoundness");
        Add("Angular", "InnerDeadzone", "OuterRingThreshold", "SnapAngleDeg", "AngularMaxZone", "AngularSquareOutput");
        foreach (PropertyDescriptor property in source)
            if (property.Category is "Controller" or "Performance") result.Add(new SettingProperty(settings, property, property.Category));
        return new PropertyDescriptorCollection(result.ToArray(), true);
    }
    public override object GetPropertyOwner(PropertyDescriptor? pd) => settings;

    sealed class SettingProperty(Settings settings, PropertyDescriptor source, string category)
        : PropertyDescriptor(category + "." + source.Name, source.Attributes.Cast<Attribute>().Where(a => a is not CategoryAttribute).Append(new CategoryAttribute(category)).ToArray())
    {
        public override string DisplayName => source.DisplayName switch { "Radial anti-deadzone" => "Anti-deadzone", "Radial max zone" => "Max zone", "Radial max output" => "Max output", _ => source.DisplayName };
        public override string Description => source.Description + (source.Name == "InnerDeadzone" ? " Shared across profiles and the right stick." : source.Name is "SquareStick" or "SquareRoundness" ? " Shared by Radial and Axial." : "");
        public override Type ComponentType => typeof(SettingsView);
        public override Type PropertyType => source.PropertyType;
        public override bool IsReadOnly => source.IsReadOnly;
        public override bool CanResetValue(object component) => source.CanResetValue(settings);
        public override object? GetValue(object? component) => source.GetValue(settings);
        public override void SetValue(object? component, object? value) { source.SetValue(settings, value); OnValueChanged(component, EventArgs.Empty); }
        public override void ResetValue(object component) => source.ResetValue(settings);
        public override bool ShouldSerializeValue(object component) => false;
    }
}
