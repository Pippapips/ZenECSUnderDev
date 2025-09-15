using Zenject;
using ZenECS.Core;

namespace ZenECS.Integration.ZenjectRx
{
    public sealed class EcsCoreInstaller : MonoInstaller
    {
        public bool BindDefaultServices = true;

        public override void InstallBindings()
        {
            Container.Bind<World>().AsSingle();
            Container.BindInterfacesTo<EcsRunnerZenject>().AsSingle().NonLazy();

            Container.Bind<IEcsMessageBus>().To<EcsMessageBus>().AsSingle();
            Container.Bind<IComponentStreamHub>().To<ComponentStreamHub>().AsSingle();

            if (BindDefaultServices)
            {
                Container.BindInterfacesTo<TimeService>().AsSingle();
                Container.BindInterfacesTo<InputService>().AsSingle();
            }
        }
    }
}