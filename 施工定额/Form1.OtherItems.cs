namespace 施工定额
{
    public partial class Form1
    {
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _otherProjectCoordinator.EnsureInitialized();
        }

        private void LoadOtherItems() => _otherProjectCoordinator.Load();
    }
}
