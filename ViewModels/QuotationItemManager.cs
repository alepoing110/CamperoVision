using System.Collections.ObjectModel;
using System.Globalization;
using CamperoDesktop.Data;
using CamperoDesktop.Models;
using CamperoDesktop.Services;

namespace CamperoDesktop.ViewModels;

public class QuotationItemManager
{
    private readonly QuotationViewModel _parent;
    private readonly IDialogService _dialogService;
    private readonly KitRepository _kitRepository;

    public QuotationItemManager(QuotationViewModel parent, IDialogService dialogService, KitRepository kitRepository)
    {
        _parent = parent;
        _dialogService = dialogService;
        _kitRepository = kitRepository;
    }

    public async Task AddCurrentSelectionAsync()
    {
        _parent.ResolveProductSelectionPublic(_parent.SearchText.Trim());
        _parent.ValidateQuantityPublic();
        _parent.ValidateItemDiscountPublic();
        if (_parent.HasErrors)
        {
            _dialogService.ShowWarning("Verifica todos los campos del item antes de agregarlo o cambiarlo");
            return;
        }

        ProductOption? product = _parent.MatchedProductPublic ?? _parent.SelectedProductPublic;
        if (product is null)
        {
            _dialogService.ShowWarning("Selecciona un producto o kit.");
            return;
        }

        if (!int.TryParse(_parent.Quantity, out int quantity) || quantity <= 0)
        {
            _dialogService.ShowWarning("Ingresa una cantidad valida.");
            return;
        }

        if (!decimal.TryParse(_parent.ItemDiscount, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal discountValue) &&
            !decimal.TryParse(_parent.ItemDiscount, out discountValue))
        {
            _dialogService.ShowWarning("Descuento invalido.");
            return;
        }

        decimal baseSubtotal = quantity * product.PrecioVenta;
        if (_parent.SelectedDiscountType == "Porcentaje" && discountValue > 100)
        {
            _dialogService.ShowWarning("El descuento porcentual no puede ser mayor a 100.");
            return;
        }

        if (_parent.SelectedDiscountType == "Monto" && discountValue > baseSubtotal)
        {
            _dialogService.ShowWarning("El descuento no puede ser mayor al subtotal del item.");
            return;
        }

        if (product.IsKit)
        {
            await AddKitSelectionAsync(product, quantity, discountValue, _parent.SelectedDetailPublic);
            return;
        }

        SaleDetailDraft? editingItem = _parent.SelectedDetailPublic;
        SaleDetailDraft? existingItem = _parent.Items.Where(i => i != editingItem)
            .FirstOrDefault(i =>
                i.IsKit == product.IsKit &&
                i.IdProducto == product.IdProducto &&
                i.IdKit == product.IdKit &&
                i.TipoDescuento == _parent.SelectedDiscountType &&
                i.DescuentoValor == discountValue);
        if (editingItem is not null)
        {
            if (existingItem is not null)
            {
                existingItem.Cantidad += quantity;
                _parent.Items.Remove(editingItem);
            }
            else
            {
                UpdateDraft(editingItem, product, quantity, discountValue);
            }
        }
        else
        {
            if (existingItem is not null)
            {
                existingItem.Cantidad += quantity;
            }
            else
            {
                _parent.Items.Add(new SaleDetailDraft());
                UpdateDraft(_parent.Items[^1], product, quantity, discountValue);
            }
        }

        _parent.OnPropertyChangedPublic(nameof(_parent.Items));
        _parent.ResetEditorPublic();
        _parent.UpdateTotalsPublic();
    }

    public async Task AddKitSelectionAsync(ProductOption kit, int kitQuantity, decimal discountValue, SaleDetailDraft? editingItem)
    {
        if (!kit.IdKit.HasValue)
        {
            _dialogService.ShowWarning("El kit seleccionado no tiene un identificador valido.");
            return;
        }

        List<KitComponentDraft> components = await _kitRepository.GetKitComponentsAsync(kit.IdKit.Value, _parent.SelectedWarehousePublic?.IdAlmacen);
        components = components.Where(component => component.Cantidad > 0).ToList();
        if (components.Count == 0)
        {
            _dialogService.ShowWarning("El kit no tiene componentes configurados.");
            return;
        }

        if (editingItem is not null)
        {
            _parent.Items.Remove(editingItem);
        }

        decimal targetBaseTotal = kit.PrecioVenta * kitQuantity;
        decimal referenceBaseTotal = components.Sum(component => component.PrecioVenta * component.Cantidad * kitQuantity);
        decimal remainingBase = targetBaseTotal;
        decimal remainingDiscount = _parent.SelectedDiscountType == "Monto" ? discountValue : 0m;

        for (int index = 0; index < components.Count; index++)
        {
            KitComponentDraft component = components[index];
            int lineQuantity = component.Cantidad * kitQuantity;
            decimal referenceLineBase = component.PrecioVenta * lineQuantity;
            decimal lineBase = index == components.Count - 1
                ? remainingBase
                : referenceBaseTotal <= 0
                    ? 0
                    : Math.Round(targetBaseTotal * referenceLineBase / referenceBaseTotal, 2, MidpointRounding.AwayFromZero);
            remainingBase -= lineBase;

            decimal lineUnitPrice = lineQuantity <= 0 ? 0 : lineBase / lineQuantity;
            string lineDiscountType = _parent.SelectedDiscountType;
            decimal lineDiscountValue;

            if (_parent.SelectedDiscountType == "Porcentaje")
            {
                lineDiscountValue = discountValue;
            }
            else
            {
                lineDiscountValue = index == components.Count - 1
                    ? remainingDiscount
                    : targetBaseTotal <= 0
                        ? 0
                        : Math.Round(discountValue * lineBase / targetBaseTotal, 2, MidpointRounding.AwayFromZero);
                lineDiscountValue = Math.Min(lineDiscountValue, lineBase);
                remainingDiscount -= lineDiscountValue;
            }

            AddOrMergeExpandedComponentDraft(component, kit, lineQuantity, lineUnitPrice, lineDiscountType, lineDiscountValue);
        }

        _parent.OnPropertyChangedPublic(nameof(_parent.Items));
        _parent.ResetEditorPublic();
        _parent.UpdateTotalsPublic();
    }

    private void AddOrMergeExpandedComponentDraft(KitComponentDraft component, ProductOption kit, int quantity, decimal unitPrice, string discountType, decimal discountValue)
    {
        SaleDetailDraft? existingItem = _parent.Items.FirstOrDefault(item =>
            !item.IsKit &&
            item.IdProducto == component.IdProducto &&
            item.TipoDescuento == discountType &&
            item.DescuentoValor == discountValue &&
            item.PrecioUnitario == unitPrice);

        if (existingItem is not null)
        {
            existingItem.Cantidad += quantity;
            return;
        }

        SaleDetailDraft draft = new();
        draft.IdProducto = component.IdProducto;
        draft.IdKit = null;
        draft.IsKit = false;
        draft.Codigo = component.Codigo;
        draft.CodigoBarras = string.Empty;
        draft.Producto = component.Nombre;
        draft.Descripcion = $"{component.Nombre} | Kit: {kit.Nombre}";
        draft.UnidadMedida = component.UnidadMedida;
        draft.Cantidad = quantity;
        draft.PrecioUnitario = unitPrice;
        draft.TipoDescuento = discountType;
        draft.DescuentoValor = discountValue;
        _parent.Items.Add(draft);
    }

    public void RemoveSelectedItem()
    {
        if (_parent.SelectedDetailPublic is null)
        {
            return;
        }

        _parent.Items.Remove(_parent.SelectedDetailPublic);
        _parent.ResetEditorPublic();
        _parent.UpdateTotalsPublic();
    }

    private void UpdateDraft(SaleDetailDraft draft, ProductOption product, int quantity, decimal discountValue)
    {
        draft.IdProducto = product.IdProducto;
        draft.IdKit = product.IdKit;
        draft.IsKit = product.IsKit;
        draft.Codigo = product.Codigo;
        draft.CodigoBarras = product.CodigoBarras;
        draft.Producto = product.Nombre;
        draft.Descripcion = string.IsNullOrWhiteSpace(product.Descripcion) ? product.Nombre : product.Descripcion;
        draft.UnidadMedida = product.UnidadMedida;
        draft.Cantidad = quantity;
        draft.PrecioUnitario = product.PrecioVenta;
        draft.TipoDescuento = _parent.SelectedDiscountType;
        draft.DescuentoValor = discountValue;
    }
}
