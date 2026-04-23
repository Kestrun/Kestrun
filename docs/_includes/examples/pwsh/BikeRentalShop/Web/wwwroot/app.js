(function () {
    const appRoot = document.querySelector('[data-bike-rental-app]');
    if (!appRoot) {
        return;
    }

    const configElement = document.getElementById('bike-rental-config');
    const config = configElement ? JSON.parse(configElement.textContent) : {};
    const statusSurface = document.querySelector('[data-status-surface]');
    const pageName = appRoot.dataset.page;

    const setStatus = (tone, title, detail) => {
        if (!statusSurface) {
            return;
        }

        const toneClass = tone || 'success';
        statusSurface.innerHTML = [
            '<div>',
            `<p class="status-title"><span class="status-pill ${toneClass}">${toneClass}</span> ${title}</p>`,
            `<p class="status-detail">${detail}</p>`,
            '</div>'
        ].join('');
    };

    const parseJsonResponse = async (response) => {
        const contentType = response.headers.get('content-type') || '';
        if (contentType.indexOf('application/json') === -1) {
            return null;
        }

        return await response.json();
    };

    const resolveErrorMessage = async (response) => {
        const payload = await parseJsonResponse(response);
        if (payload && payload.error) {
            return payload.error;
        }

        return `${response.status} ${response.statusText}`;
    };

    const requestJson = async (path, options) => {
        const response = await fetch(`${config.apiBaseUrl}${path}`, options || {});
        if (!response.ok) {
            throw new Error(await resolveErrorMessage(response));
        }

        const payload = await parseJsonResponse(response);
        return payload;
    };

    const toCurrency = (value) => {
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD'
        }).format(Number(value || 0));
    };

    const renderCatalog = (targetId, bikes) => {
        const target = document.getElementById(targetId);
        if (!target) {
            return;
        }

        if (!bikes || bikes.length === 0) {
            target.innerHTML = '<p class="result-placeholder">No bikes match the current filter.</p>';
            return;
        }

        target.innerHTML = bikes.map((bike) => {
            return [
                '<article class="bike-card">',
                '<div class="bike-topline">',
                `<h3>${bike.model}</h3>`,
                `<span class="bike-tag ${bike.status}">${bike.status}</span>`,
                '</div>',
                `<p><strong>${bike.bikeId}</strong> <span class="info-chip">${bike.type}</span></p>`,
                '<div class="bike-meta">',
                `<span>Rate: ${toCurrency(bike.hourlyRate)} / hour</span>`,
                `<span>Dock: ${bike.dock}</span>`,
                '</div>',
                '</article>'
            ].join('');
        }).join('');
    };

    const renderRentalResult = (targetId, rental) => {
        const target = document.getElementById(targetId);
        if (!target) {
            return;
        }

        target.innerHTML = [
            '<dl>',
            `<dt>Rental</dt><dd>${rental.rentalId}</dd>`,
            `<dt>Bike</dt><dd>${rental.bikeId} - ${rental.bikeModel}</dd>`,
            `<dt>Status</dt><dd>${rental.status}</dd>`,
            `<dt>Pickup code</dt><dd>${rental.pickupCode || 'n/a'}</dd>`,
            `<dt>Due</dt><dd>${rental.dueAtUtc || 'n/a'}</dd>`,
            `<dt>Estimate</dt><dd>${toCurrency(rental.totalEstimate)}</dd>`,
            '</dl>'
        ].join('');
    };

    const getStaffHeaders = () => {
        const keyInput = document.getElementById('staff-api-key');
        const staffKey = keyInput && keyInput.value ? keyInput.value.trim() : (config.demoApiKey || '');
        return {
            'Content-Type': 'application/json',
            'X-Api-Key': staffKey
        };
    };

    const loadHome = async () => {
        const [bikes, health] = await Promise.all([
            requestJson(config.publicCatalogPath),
            requestJson(config.healthPath)
        ]);

        renderCatalog('bike-catalog', bikes);
        document.getElementById('available-count').textContent = String(bikes.filter((bike) => bike.status === 'available').length);
        document.getElementById('active-count').textContent = String(health.activeRentals);
        document.getElementById('health-status').textContent = health.status;
    };

    const loadOperations = async () => {
        const [dashboard, bikes] = await Promise.all([
            requestJson(config.staffDashboardPath, { headers: getStaffHeaders() }),
            requestJson(config.publicCatalogPath)
        ]);

        document.getElementById('ops-total').textContent = String(dashboard.inventory.total);
        document.getElementById('ops-available').textContent = String(dashboard.inventory.available);
        document.getElementById('ops-rented').textContent = String(dashboard.inventory.rented);
        document.getElementById('ops-active-rentals').textContent = String(dashboard.rentals.active);

        const rentalList = document.getElementById('active-rental-list');
        rentalList.innerHTML = dashboard.rentals.activeIds.length
            ? dashboard.rentals.activeIds.map((rentalId) => `<li>${rentalId}</li>`).join('')
            : '<li class="result-placeholder">No active rentals right now.</li>';

        renderCatalog('operations-catalog', bikes);
    };

    const bindHomeForms = () => {
        const bookingForm = document.getElementById('booking-form');
        const lookupForm = document.getElementById('lookup-form');
        const refreshButton = document.getElementById('refresh-home');

        bookingForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            const formData = new FormData(bookingForm);
            const payload = {
                bikeId: formData.get('bikeId'),
                customerName: formData.get('customerName'),
                phone: formData.get('phone'),
                plannedHours: Number(formData.get('plannedHours'))
            };

            try {
                const rental = await requestJson(config.createRentalPath, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                renderRentalResult('booking-result', rental);
                document.getElementById('rental-lookup-id').value = rental.rentalId;
                await loadHome();
                setStatus('success', 'Rental created', `Pickup code ${rental.pickupCode} is ready for ${rental.customerName}.`);
            } catch (error) {
                setStatus('error', 'Booking failed', error.message);
            }
        });

        lookupForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            const rentalId = new FormData(lookupForm).get('rentalId');
            try {
                const rental = await requestJson(`${config.rentalStatusBasePath}/${encodeURIComponent(rentalId)}`);
                renderRentalResult('lookup-result', rental);
                setStatus('success', 'Rental located', `Rental ${rental.rentalId} is currently ${rental.status}.`);
            } catch (error) {
                setStatus('error', 'Lookup failed', error.message);
            }
        });

        refreshButton.addEventListener('click', async () => {
            try {
                await loadHome();
                setStatus('success', 'Catalog refreshed', 'Pulled the latest bike availability and shop health from the API.');
            } catch (error) {
                setStatus('error', 'Refresh failed', error.message);
            }
        });
    };

    const bindOperationsForms = () => {
        const addBikeForm = document.getElementById('add-bike-form');
        const returnRentalForm = document.getElementById('return-rental-form');
        const removeBikeForm = document.getElementById('remove-bike-form');
        const refreshButton = document.getElementById('refresh-operations');

        addBikeForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            const formData = new FormData(addBikeForm);
            const payload = {
                bikeId: formData.get('bikeId'),
                model: formData.get('model'),
                type: formData.get('type'),
                hourlyRate: Number(formData.get('hourlyRate')),
                dock: formData.get('dock'),
                lastServiceDate: formData.get('lastServiceDate')
            };

            try {
                const created = await requestJson(config.addBikePath, {
                    method: 'POST',
                    headers: getStaffHeaders(),
                    body: JSON.stringify(payload)
                });
                addBikeForm.reset();
                await loadOperations();
                setStatus('success', 'Bike added', `${created.bikeId} is now available in the inventory.`);
            } catch (error) {
                setStatus('error', 'Add bike failed', error.message);
            }
        });

        returnRentalForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            const formData = new FormData(returnRentalForm);
            const rentalId = formData.get('rentalId');
            const payload = {
                conditionNotes: formData.get('conditionNotes')
            };

            try {
                const rental = await requestJson(`${config.returnRentalBasePath}/${encodeURIComponent(rentalId)}/return`, {
                    method: 'POST',
                    headers: getStaffHeaders(),
                    body: JSON.stringify(payload)
                });
                returnRentalForm.reset();
                await loadOperations();
                setStatus('success', 'Rental closed', `${rental.rentalId} is now marked ${rental.status}.`);
            } catch (error) {
                setStatus('error', 'Return failed', error.message);
            }
        });

        removeBikeForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            const bikeId = new FormData(removeBikeForm).get('bikeId');
            try {
                const removed = await requestJson(`${config.removeBikeBasePath}/${encodeURIComponent(bikeId)}`, {
                    method: 'DELETE',
                    headers: { 'X-Api-Key': getStaffHeaders()['X-Api-Key'] }
                });
                removeBikeForm.reset();
                await loadOperations();
                setStatus('success', 'Bike removed', `${removed.bikeId} was removed from inventory.`);
            } catch (error) {
                setStatus('error', 'Remove failed', error.message);
            }
        });

        refreshButton.addEventListener('click', async () => {
            try {
                await loadOperations();
                setStatus('success', 'Operations refreshed', 'Dashboard counts and inventory were pulled from the live API.');
            } catch (error) {
                setStatus('error', 'Refresh failed', error.message);
            }
        });
    };

    const initialize = async () => {
        try {
            if (pageName === 'operations') {
                bindOperationsForms();
                await loadOperations();
            } else {
                bindHomeForms();
                await loadHome();
            }

            setStatus('success', 'API connected', 'The web client loaded and completed its first round of live backend requests.');
        } catch (error) {
            setStatus('error', 'Startup failed', error.message);
        }
    };

    initialize();
})();
