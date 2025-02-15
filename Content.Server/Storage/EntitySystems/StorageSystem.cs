using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Explosion;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared.Lock;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Storage.EntitySystems;

public sealed partial class StorageSystem : SharedStorageSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StorageComponent, BeforeExplodeEvent>(OnExploded);

        SubscribeLocalEvent<StorageFillComponent, MapInitEvent>(OnStorageFillMapInit);
    }

    private void AddUiVerb(EntityUid uid, StorageComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        var silent = false;
        if (!args.CanAccess || !args.CanInteract || TryComp<LockComponent>(uid, out var lockComponent) && lockComponent.Locked)
        {
            // we allow admins to open the storage anyways
            if (!_admin.HasAdminFlag(args.User, AdminFlags.Admin))
                return;

            silent = true;
        }

        silent |= HasComp<GhostComponent>(args.User);

        // Get the session for the user
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        // Does this player currently have the storage UI open?
        var uiOpen = _uiSystem.SessionHasOpenUi(uid, StorageComponent.StorageUiKey.Key, actor.PlayerSession);

        ActivationVerb verb = new()
        {
            Act = () =>
            {
                if (uiOpen)
                {
                    _uiSystem.TryClose(uid, StorageComponent.StorageUiKey.Key, actor.PlayerSession);
                }
                else
                {
                    OpenStorageUI(uid, args.User, component, silent);
                }
            }
        };
        if (uiOpen)
        {
            verb.Text = Loc.GetString("comp-storage-verb-close-storage");
            verb.Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/close.svg.192dpi.png"));
        }
        else
        {
            verb.Text = Loc.GetString("comp-storage-verb-open-storage");
            verb.Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"));
        }
        args.Verbs.Add(verb);
    }

    private void OnBoundUIClosed(EntityUid uid, StorageComponent storageComp, BoundUIClosedEvent args)
    {
        if (TryComp<ActorComponent>(args.Session.AttachedEntity, out var actor) && actor?.PlayerSession != null)
            CloseNestedInterfaces(uid, actor.PlayerSession, storageComp);

        // If UI is closed for everyone
        if (!_uiSystem.IsUiOpen(uid, args.UiKey))
        {
            storageComp.IsUiOpen = false;
            UpdateAppearance((uid, storageComp, null));

            if (storageComp.StorageCloseSound is not null)
                Audio.PlayEntity(storageComp.StorageCloseSound, Filter.Pvs(uid, entityManager: EntityManager), uid, true, storageComp.StorageCloseSound.Params);
        }
    }

    private void OnExploded(Entity<StorageComponent> ent, ref BeforeExplodeEvent args)
    {
        args.Contents.AddRange(ent.Comp.Container.ContainedEntities);
    }

    /// <inheritdoc />
    public override void PlayPickupAnimation(EntityUid uid, EntityCoordinates initialCoordinates, EntityCoordinates finalCoordinates,
        Angle initialRotation, EntityUid? user = null)
    {
        var filter = Filter.Pvs(uid).RemoveWhereAttachedEntity(e => e == user);
        RaiseNetworkEvent(new PickupAnimationEvent(GetNetEntity(uid), GetNetCoordinates(initialCoordinates), GetNetCoordinates(finalCoordinates), initialRotation), filter);
    }
}
