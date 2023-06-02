const mongodbRest = require('mongodb-rest');

mongodbRest.init({
  dbName: 'VideoEdting',
  dbHost: 'localhost',
  dbPort: 27017
}, (err, db) => {
  if (err) throw err;
  
  mongodbRest.serve({
    db,
    router: mongodbRest.router,
    port: 28017
  });
});