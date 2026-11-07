(() => {
    const rows = document.getElementById('averias-damages');
    const catalog = JSON.parse(document.getElementById('averias-elements').textContent);
    const template = rows.firstElementChild.cloneNode(true);

    function updateElements(row, selected = '') {
        const category = row.querySelector('[data-field="Category"]');
        const element = row.querySelector('[data-field="Element"]');
        element.replaceChildren(new Option(category.value
            ? 'Selecciona el elemento dañado'
            : 'Selecciona primero una categoría', ''));
        for (const value of catalog[category.value] || []) {
            element.add(new Option(value, value, false, value === selected));
        }
        element.disabled = !category.value;
    }

    function refreshRows() {
        Array.from(rows.children).forEach((row, index) => {
            for (const field of row.querySelectorAll('[data-field]')) {
                field.name = `Damages[${index}].${field.dataset.field}`;
            }
            row.querySelector('[data-remove-damage]').disabled = rows.children.length === 1;
        });
    }

    for (const row of rows.children) {
        updateElements(row, row.querySelector('[data-field="Element"]').dataset.selected);
    }
    refreshRows();

    rows.addEventListener('change', event => {
        if (event.target.dataset.field === 'Category') {
            updateElements(event.target.closest('[data-damage]'));
        }
    });
    rows.addEventListener('click', event => {
        if (event.target.matches('[data-remove-damage]') && rows.children.length > 1) {
            event.target.closest('[data-damage]').remove();
            refreshRows();
            document.getElementById('averias-add-damage').focus();
        }
    });
    document.getElementById('averias-add-damage').addEventListener('click', () => {
        const row = template.cloneNode(true);
        for (const select of row.querySelectorAll('select')) select.value = '';
        updateElements(row);
        rows.append(row);
        refreshRows();
        row.querySelector('select').focus();
    });
})();
